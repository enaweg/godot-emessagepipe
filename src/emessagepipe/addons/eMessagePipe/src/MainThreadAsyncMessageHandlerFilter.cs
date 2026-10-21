using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MessagePipe;

namespace Enaweg.MessagePipe;

/// <summary>
/// Runs async message handlers on Godot's main thread, so they can touch nodes safely when a
/// message is published from another thread.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
/// <remarks>
/// A message published on the main thread is handled immediately. One published elsewhere is
/// handed to the main thread with <c>CallDeferred</c>; the returned task completes once the
/// handler has finished there, so awaiting <c>PublishAsync</c> still waits for the handler.
/// Cancelling the token while the handler is still waiting for the main thread cancels the task
/// right away, and the handler is then skipped.
/// <para>
/// Pass an instance per subscription, <c>subscriber.Subscribe(handler, new MainThreadAsyncMessageHandlerFilter&lt;T&gt; { Owner = this })</c>,
/// or register it for every message type with
/// <c>options.AddGlobalAsyncMessageHandlerFilter(typeof(MainThreadAsyncMessageHandlerFilter&lt;&gt;))</c>.
/// </para>
/// </remarks>
public sealed class MainThreadAsyncMessageHandlerFilter<T> : AsyncMessageHandlerFilter<T>
{
    /// <summary>
    /// The object the handler belongs to, usually the subscribing node. Once it has been freed the
    /// handler is skipped and the task completes, since a deferred message can still arrive after
    /// its subscription was disposed together with the node.
    /// </summary>
    public GodotObject? Owner { get; init; }

    /// <inheritdoc />
    public override ValueTask HandleAsync(
        T message,
        CancellationToken cancellationToken,
        Func<T, CancellationToken, ValueTask> next)
    {
        if (GodotThread.IsMainThread())
        {
            return IsOwnerAlive ? next(message, cancellationToken) : default;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        // A statement lambda, so Callable.From does not try to return the Task to Godot as a Variant.
        Callable.From(() => { _ = RunAsync(message, cancellationToken, next, completion, registration); }).CallDeferred();
        return new ValueTask(completion.Task);
    }

    async Task RunAsync(
        T message,
        CancellationToken cancellationToken,
        Func<T, CancellationToken, ValueTask> next,
        TaskCompletionSource completion,
        CancellationTokenRegistration registration)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            if (IsOwnerAlive)
            {
                await next(message, cancellationToken);
            }

            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            registration.Dispose();
        }
    }

    bool IsOwnerAlive => Owner is null || GodotObject.IsInstanceValid(Owner);
}
