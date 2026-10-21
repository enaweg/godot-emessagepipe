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
/// <see cref="MainThreadAsyncMessageHandlerFilter{T}"/> wired through the VContainer bridge, per
/// subscription and as global filters.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainThreadMessageHandlerFilterContainerTest
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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
