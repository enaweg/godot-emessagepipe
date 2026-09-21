using System;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Covers the <see cref="IServiceProvider"/> bridge MessagePipe resolves filters and handlers
/// through — <c>ObjectResolverProxy</c> and <c>ObjectResolverExtensions.AsServiceProvider</c>.
/// </summary>
[TestSuite]
public class ObjectResolverProxyTest
{
	static IObjectResolver BuildContainer()
	{
		var builder = new ContainerBuilder();
		builder.Register<Recorder>(Lifetime.Singleton);
		return builder.Build();
	}

	[TestCase]
	public void GetService_ResolvesRegisteredService()
	{
		using var container = BuildContainer();

		var provider = container.AsServiceProvider();

		AssertThat(provider.GetService(typeof(Recorder))).IsSame(container.Resolve<Recorder>());
	}

	/// <summary>
	/// <see cref="IServiceProvider.GetService"/> is contractually null-for-unregistered; it used to
	/// throw a <c>VContainerException</c>, which broke callers probing for optional services.
	/// </summary>
	[TestCase]
	public void GetService_ReturnsNullForUnregisteredService()
	{
		using var container = BuildContainer();

		var provider = container.AsServiceProvider();

		AssertThat(provider.GetService(typeof(Ping))).IsNull();
	}

	/// <summary><c>RegisterMessagePipe</c> registers the provider as scoped, so it must follow the scope.</summary>
	[TestCase]
	public void RegisteredProvider_ResolvesFromItsOwnScope()
	{
		var builder = new ContainerBuilder();
		builder.RegisterMessagePipe();

		using var root = builder.Build();
		using var child = root.CreateScope(childBuilder => childBuilder.Register<Recorder>(Lifetime.Scoped));

		AssertThat(child.Resolve<IServiceProvider>().GetService(typeof(Recorder))).IsNotNull();
		AssertThat(root.Resolve<IServiceProvider>().GetService(typeof(Recorder))).IsNull();
	}
}
