using System;
using Godot;
using MessagePipe;

namespace Enaweg.MessagePipe;

/// <summary>
/// Runs message handlers on Godot's main thread, so they can touch nodes safely when a message is
/// published from another thread.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
/// <remarks>
/// A message published on the main thread is handled immediately. One published elsewhere is
/// handed to the main thread with <c>CallDeferred</c> and handled at the end of the current frame,
/// so <c>Publish</c> returns before the handler has run.
/// <para>
/// Pass an instance per subscription, <c>subscriber.Subscribe(handler, new MainThreadMessageHandlerFilter&lt;T&gt;())</c>,
/// or register it for every message type with
/// <c>options.AddGlobalMessageHandlerFilter(typeof(MainThreadMessageHandlerFilter&lt;&gt;))</c>.
/// </para>
/// </remarks>
public sealed class MainThreadMessageHandlerFilter<T> : MessageHandlerFilter<T>
{
    /// <inheritdoc />
    public override void Handle(T message, Action<T> next)
    {
        if (GodotThread.IsMainThread())
        {
            next(message);
            return;
        }

        Callable.From(() => next(message)).CallDeferred();
    }
}
