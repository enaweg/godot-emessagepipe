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
/// <para>
/// Pass an instance per subscription, <c>subscriber.Subscribe(handler, new MainThreadAsyncMessageHandlerFilter&lt;T&gt;())</c>,
/// or register it for every message type with
/// <c>options.AddGlobalAsyncMessageHandlerFilter(typeof(MainThreadAsyncMessageHandlerFilter&lt;&gt;))</c>.
/// </para>
/// </remarks>
public sealed class MainThreadAsyncMessageHandlerFilter<T> : AsyncMessageHandlerFilter<T>
{
    /// <inheritdoc />
    public override ValueTask HandleAsync(
        T message,
        CancellationToken cancellationToken,
        Func<T, CancellationToken, ValueTask> next)
    {
        if (GodotThread.IsMainThread())
        {
            return next(message, cancellationToken);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Callable.From(() => _ = RunAsync(message, cancellationToken, next, completion)).CallDeferred();
        return new ValueTask(completion.Task);
    }

    static async Task RunAsync(
        T message,
        CancellationToken cancellationToken,
        Func<T, CancellationToken, ValueTask> next,
        TaskCompletionSource completion)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
            return;
        }

        try
        {
            await next(message, cancellationToken);
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
    }
}
