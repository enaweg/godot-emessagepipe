using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using MessagePipe;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Covers <see cref="MainThreadMessageHandlerFilter{T}"/> and
/// <see cref="MainThreadAsyncMessageHandlerFilter{T}"/> — moving handlers onto Godot's main thread.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainThreadMessageHandlerFilterTest
{
	static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

	/// <summary>On the main thread the handler runs before <c>Handle</c> returns.</summary>
	[TestCase]
	public async Task Handle_RunsImmediatelyOnTheMainThread()
	{
		var filter = new MainThreadMessageHandlerFilter<Ping>();
		var handledBeforeReturn = false;

		await GodotFrames.RunOnMainThread(() =>
		{
			var handled = false;
			filter.Handle(new Ping("main"), _ => handled = true);
			handledBeforeReturn = handled;
		}).WaitAsync(Timeout);

		AssertThat(handledBeforeReturn).IsTrue();
	}

	/// <summary>From a worker thread the handler is deferred to the main thread.</summary>
	[TestCase]
	public async Task Handle_DefersToTheMainThreadFromAWorkerThread()
	{
		var filter = new MainThreadMessageHandlerFilter<Ping>();
		var handledOnMainThread = new ConcurrentQueue<bool>();

		await Task.Run(() => filter.Handle(
			new Ping("worker"),
			_ => handledOnMainThread.Enqueue(GodotThread.IsMainThread())));
		await GodotFrames.WaitUntil(() => !handledOnMainThread.IsEmpty);

		AssertThat(new List<bool>(handledOnMainThread)).ContainsExactly(true);
	}

	/// <summary>
	/// From a worker thread the async handler runs on the main thread, and the returned task
	/// completes only after it has finished.
	/// </summary>
	[TestCase]
	public async Task HandleAsync_CompletesAfterTheHandlerRanOnTheMainThread()
	{
		var filter = new MainThreadAsyncMessageHandlerFilter<Ping>();
		var handledOnMainThread = new ConcurrentQueue<bool>();

		await Task.Run(async () => await filter.HandleAsync(
			new Ping("worker"),
			default,
			(_, _) =>
			{
				handledOnMainThread.Enqueue(GodotThread.IsMainThread());
				return default;
			})).WaitAsync(Timeout);

		AssertThat(new List<bool>(handledOnMainThread)).ContainsExactly(true);
	}

	/// <summary>A failing async handler surfaces through the returned task.</summary>
	[TestCase]
	public async Task HandleAsync_PropagatesHandlerExceptions()
	{
		var filter = new MainThreadAsyncMessageHandlerFilter<Ping>();

		Exception? exception = null;
		try
		{
			await Task.Run(async () => await filter.HandleAsync(
				new Ping("worker"),
				default,
				(_, _) => throw new InvalidOperationException("handler failed"))).WaitAsync(Timeout);
		}
		catch (Exception caught)
		{
			exception = caught;
		}

		AssertThat(exception).IsNotNull();
		AssertThat(exception!.GetType().Name).IsEqual(nameof(InvalidOperationException));
	}

	/// <summary>A filter instance passed to <c>Subscribe</c> applies to that subscription.</summary>
	[TestCase]
	public async Task Subscribe_WithAFilterInstanceHandlesOnTheMainThread()
	{
		var builder = new ContainerBuilder();
		builder.RegisterMessagePipe();
		using var container = builder.Build();
		var publisher = container.Resolve<IPublisher<Ping>>();
		var handledOnMainThread = new ConcurrentQueue<bool>();

		using var subscription = container.Resolve<ISubscriber<Ping>>().Subscribe(
			_ => handledOnMainThread.Enqueue(GodotThread.IsMainThread()),
			new MainThreadMessageHandlerFilter<Ping>());

		await Task.Run(() => publisher.Publish(new Ping("worker")));
		await GodotFrames.WaitUntil(() => !handledOnMainThread.IsEmpty);

		AssertThat(new List<bool>(handledOnMainThread)).ContainsExactly(true);
	}

	/// <summary>
	/// Registered as an open-generic global filter, it applies to every message type — which relies
	/// on the VContainer bridge registering MessagePipe's global filters.
	/// </summary>
	[TestCase]
	public async Task GlobalFilter_HandlesEveryMessageTypeOnTheMainThread()
	{
		var builder = new ContainerBuilder();
		builder.RegisterMessagePipe(o => o.AddGlobalMessageHandlerFilter(typeof(MainThreadMessageHandlerFilter<>)));
		using var container = builder.Build();
		var pingPublisher = container.Resolve<IPublisher<Ping>>();
		var textPublisher = container.Resolve<IPublisher<string>>();
		var handledOnMainThread = new ConcurrentQueue<bool>();

		using var pingSubscription = container.Resolve<ISubscriber<Ping>>()
			.Subscribe(_ => handledOnMainThread.Enqueue(GodotThread.IsMainThread()));
		using var textSubscription = container.Resolve<ISubscriber<string>>()
			.Subscribe(_ => handledOnMainThread.Enqueue(GodotThread.IsMainThread()));

		await Task.Run(() =>
		{
			pingPublisher.Publish(new Ping("worker"));
			textPublisher.Publish("worker");
		});
		await GodotFrames.WaitUntil(() => handledOnMainThread.Count == 2);

		AssertThat(new List<bool>(handledOnMainThread)).ContainsExactly(true, true);
	}

	/// <summary>The async filter also works as an open-generic global filter.</summary>
	[TestCase]
	public async Task GlobalAsyncFilter_HandlesOnTheMainThread()
	{
		var builder = new ContainerBuilder();
		builder.RegisterMessagePipe(o => o.AddGlobalAsyncMessageHandlerFilter(typeof(MainThreadAsyncMessageHandlerFilter<>)));
		using var container = builder.Build();
		var publisher = container.Resolve<IAsyncPublisher<Ping>>();
		var handledOnMainThread = new ConcurrentQueue<bool>();

		using var subscription = container.Resolve<IAsyncSubscriber<Ping>>().Subscribe((_, _) =>
		{
			handledOnMainThread.Enqueue(GodotThread.IsMainThread());
			return default;
		});

		await Task.Run(async () => await publisher.PublishAsync(new Ping("worker"))).WaitAsync(Timeout);

		AssertThat(new List<bool>(handledOnMainThread)).ContainsExactly(true);
	}
}
