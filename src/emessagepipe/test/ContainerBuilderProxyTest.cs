using System;
using GdUnit4;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Covers the <see cref="IServiceCollection"/> facade returned by
/// <see cref="ContainerBuilderExtensions.AsServiceCollection"/>, which translates
/// <see cref="ServiceDescriptor"/>s into VContainer registrations.
/// </summary>
/// <remarks>
/// The facade previously inherited <c>ServiceCollection</c>, so descriptors were stored and never
/// reached VContainer. Every descriptor shape is asserted here because that failure was silent.
/// </remarks>
[TestSuite]
public class ContainerBuilderProxyTest
{
	static Exception? Capture(Action action)
	{
		try
		{
			action();
			return null;
		}
		catch (Exception exception)
		{
			return exception;
		}
	}

	[TestCase]
	public void Add_ForwardsImplementationTypeDescriptor()
	{
		var builder = new ContainerBuilder();
		builder.AsServiceCollection().AddSingleton<Recorder>();

		using var container = builder.Build();

		AssertThat(container.Resolve<Recorder>()).IsNotNull();
	}

	/// <summary>A descriptor whose implementation type differs must resolve under its service type.</summary>
	[TestCase]
	public void Add_ForwardsDescriptorUnderItsServiceType()
	{
		var builder = new ContainerBuilder();
		builder.AsServiceCollection().AddSingleton<IRecorder, RecorderImplementation>();

		using var container = builder.Build();

		AssertThat(container.Resolve<IRecorder>()).IsInstanceOf<RecorderImplementation>();
	}

	/// <summary>
	/// VContainer keys instance registrations off the static type argument, so the service type has
	/// to be supplied explicitly rather than inferred as <see cref="object"/>.
	/// </summary>
	[TestCase]
	public void Add_ForwardsInstanceDescriptor()
	{
		var builder = new ContainerBuilder();
		var instance = new Recorder();
		builder.AsServiceCollection().AddSingleton(instance);

		using var container = builder.Build();

		AssertThat(container.Resolve<Recorder>()).IsSame(instance);
	}

	[TestCase]
	public void Add_ForwardsFactoryDescriptor()
	{
		var builder = new ContainerBuilder();
		builder.AsServiceCollection().AddTransient<Ping>(_ => new Ping("from-factory"));

		using var container = builder.Build();

		AssertThat(container.Resolve<Ping>().Payload).IsEqual("from-factory");
	}

	/// <summary>A factory descriptor must be able to resolve its own dependencies.</summary>
	[TestCase]
	public void Add_FactoryDescriptorResolvesFromTheContainer()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();
		var instance = new Recorder();
		services.AddSingleton(instance);
		services.AddTransient<Ping>(provider => new Ping(provider.GetService(typeof(Recorder)) is null ? "missing" : "resolved"));

		using var container = builder.Build();

		AssertThat(container.Resolve<Ping>().Payload).IsEqual("resolved");
	}

	[TestCase]
	public void Add_MapsSingletonLifetime()
	{
		var builder = new ContainerBuilder();
		builder.AsServiceCollection().AddSingleton<Recorder>();

		using var container = builder.Build();

		AssertThat(container.Resolve<Recorder>()).IsSame(container.Resolve<Recorder>());
	}

	[TestCase]
	public void Add_MapsTransientLifetime()
	{
		var builder = new ContainerBuilder();
		builder.AsServiceCollection().AddTransient<Recorder>();

		using var container = builder.Build();

		AssertThat(container.Resolve<Recorder>()).IsNotSame(container.Resolve<Recorder>());
	}

	[TestCase]
	public void Add_MapsScopedLifetime()
	{
		var builder = new ContainerBuilder();
		builder.AsServiceCollection().AddScoped<Recorder>();

		using var root = builder.Build();
		using var first = root.CreateScope(_ => { });
		using var second = root.CreateScope(_ => { });

		AssertThat(first.Resolve<Recorder>()).IsSame(first.Resolve<Recorder>());
		AssertThat(first.Resolve<Recorder>()).IsNotSame(second.Resolve<Recorder>());
	}

	/// <summary>The facade stays enumerable so MessagePipe's <c>TryAdd</c>-style helpers can inspect it.</summary>
	[TestCase]
	public void Collection_TracksForwardedDescriptors()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();

		AssertThat(services.Count).IsEqual(0);

		services.AddSingleton<Recorder>();

		AssertThat(services.Count).IsEqual(1);
		AssertThat(services[0].ServiceType).IsEqual(typeof(Recorder));
		AssertThat(services.IndexOf(services[0])).IsEqual(0);
		AssertThat(services.Contains(services[0])).IsTrue();
		AssertThat(services.IsReadOnly).IsFalse();

		var copy = new ServiceDescriptor[1];
		services.CopyTo(copy, 0);
		AssertThat(copy[0]).IsSame(services[0]);
	}

	/// <summary><c>TryAdd</c> relies on both enumeration and forwarding, so it is checked end to end.</summary>
	[TestCase]
	public void Collection_TryAddSkipsDuplicatesAndStillForwards()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();

		services.TryAddSingleton<Recorder>();
		services.TryAddSingleton<Recorder>();

		using var container = builder.Build();

		AssertThat(services.Count).IsEqual(1);
		AssertThat(container.Resolve<Recorder>()).IsNotNull();
	}

	/// <summary>VContainer has no registration ordering, so an insert is an append.</summary>
	[TestCase]
	public void Collection_InsertForwardsLikeAdd()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();

		services.Insert(0, ServiceDescriptor.Singleton<Recorder, Recorder>());

		using var container = builder.Build();

		AssertThat(services.Count).IsEqual(1);
		AssertThat(container.Resolve<Recorder>()).IsNotNull();
	}

	/// <summary>
	/// VContainer cannot un-register, so removal fails loudly instead of leaving the collection and
	/// the container disagreeing about what is registered.
	/// </summary>
	[TestCase]
	public void Collection_RejectsRemoval()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();
		services.AddSingleton<Recorder>();
		var descriptor = services[0];

		AssertThat(Capture(() => services.RemoveAt(0))!.GetType().Name).IsEqual(nameof(NotSupportedException));
		AssertThat(Capture(() => services.Remove(descriptor))!.GetType().Name).IsEqual(nameof(NotSupportedException));
		AssertThat(Capture(() => services.Clear())!.GetType().Name).IsEqual(nameof(NotSupportedException));
		AssertThat(Capture(() => services[0] = descriptor)!.GetType().Name).IsEqual(nameof(NotSupportedException));
	}

	[TestCase]
	public void Collection_RejectsNullDescriptor()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();

		var exception = Capture(() => services.Add(null!));

		AssertThat(exception!.GetType().Name).IsEqual(nameof(ArgumentNullException));
	}

	/// <summary>An open generic cannot be produced by a factory, so the facade says so rather than failing later.</summary>
	[TestCase]
	public void Collection_RejectsOpenGenericFactoryDescriptor()
	{
		var builder = new ContainerBuilder();
		var services = builder.AsServiceCollection();
		var descriptor = new ServiceDescriptor(
			typeof(System.Collections.Generic.IList<>),
			_ => new System.Collections.Generic.List<int>(),
			ServiceLifetime.Transient);

		var exception = Capture(() => services.Add(descriptor));

		AssertThat(exception!.GetType().Name).IsEqual(nameof(NotSupportedException));
	}
}
