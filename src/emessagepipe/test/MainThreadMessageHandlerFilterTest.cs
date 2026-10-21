using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
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
    /// A message deferred from a worker thread can arrive after its owner was freed; the handler
    /// must be skipped then, since it would touch a freed node.
    /// </summary>
    [TestCase]
    public async Task Handle_SkipsTheHandlerOnceTheOwnerIsFreed()
    {
        var owner = new Node();
        var filter = new MainThreadMessageHandlerFilter<Ping> { Owner = owner };
        var handled = 0;

        await GodotFrames.RunOnMainThread(() =>
        {
            // The main thread is blocked here, so the deferred handler cannot run before the free.
            Task.Run(() => filter.Handle(new Ping("worker"), _ => Interlocked.Increment(ref handled))).Wait();
            owner.Free();
        }).WaitAsync(Timeout);
        await GodotFrames.WaitUntil(() => false, 2);

        AssertThat(handled).IsEqual(0);
    }

    /// <summary>On the main thread the owner check applies as well.</summary>
    [TestCase]
    public async Task Handle_SkipsTheHandlerForAFreedOwnerOnTheMainThread()
    {
        var owner = new Node();
        owner.Free();
        var filter = new MainThreadMessageHandlerFilter<Ping> { Owner = owner };
        var handled = false;

        await GodotFrames.RunOnMainThread(() => filter.Handle(new Ping("main"), _ => handled = true))
            .WaitAsync(Timeout);

        AssertThat(handled).IsFalse();
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

        var publishing = Task.Run(async () => await filter.HandleAsync(
            new Ping("worker"),
            default,
            (_, _) => throw new InvalidOperationException("handler failed"))).WaitAsync(Timeout);

        (await AssertThrown(publishing))!.IsInstanceOf<InvalidOperationException>();
    }

    /// <summary>
    /// Cancelling while the handler still waits for the main thread cancels the task right away,
    /// and the handler does not run afterwards.
    /// </summary>
    [TestCase]
    public async Task HandleAsync_CancelsWhileWaitingForTheMainThread()
    {
        var filter = new MainThreadAsyncMessageHandlerFilter<Ping>();
        using var cancellation = new CancellationTokenSource();
        var handled = 0;
        var canceledBeforeTheMainThreadRan = false;

        await GodotFrames.RunOnMainThread(() =>
        {
            // The main thread is blocked here, so the deferred handler cannot have started yet.
            Task pending = Task.CompletedTask;
            Task.Run(() =>
            {
                pending = filter.HandleAsync(new Ping("worker"), cancellation.Token, (_, _) =>
                {
                    Interlocked.Increment(ref handled);
                    return default;
                }).AsTask();
                cancellation.Cancel();
            }).Wait();
            canceledBeforeTheMainThreadRan = pending.IsCanceled;
        }).WaitAsync(Timeout);
        await GodotFrames.WaitUntil(() => false, 2);

        AssertThat(canceledBeforeTheMainThreadRan).IsTrue();
        AssertThat(handled).IsEqual(0);
    }

    /// <summary>A freed owner skips the async handler and still completes the task.</summary>
    [TestCase]
    public async Task HandleAsync_SkipsTheHandlerOnceTheOwnerIsFreed()
    {
        var owner = new Node();
        var filter = new MainThreadAsyncMessageHandlerFilter<Ping> { Owner = owner };
        var handled = 0;
        Task pending = Task.CompletedTask;

        await GodotFrames.RunOnMainThread(() =>
        {
            // A statement lambda: returning the task would make Task.Run wait for the handler,
            // which needs the main thread this blocks.
            Task.Run(() =>
            {
                pending = filter.HandleAsync(new Ping("worker"), default, (_, _) =>
                {
                    Interlocked.Increment(ref handled);
                    return default;
                }).AsTask();
            }).Wait();
            owner.Free();
        }).WaitAsync(Timeout);
        await pending.WaitAsync(Timeout);

        AssertThat(handled).IsEqual(0);
    }
}
