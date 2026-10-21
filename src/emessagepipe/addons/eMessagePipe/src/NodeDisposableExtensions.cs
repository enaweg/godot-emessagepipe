using System;
using Godot;

namespace Enaweg.MessagePipe;

/// <summary>
/// Ties the lifetime of a subscription, or any other <see cref="IDisposable"/>, to a Godot node.
/// </summary>
public static class NodeDisposableExtensions
{
    /// <summary>
    /// Disposes <paramref name="disposable"/> when <paramref name="node"/> is freed, for example
    /// <c>subscriber.Subscribe(OnMessage).AddTo(this)</c>.
    /// </summary>
    /// <typeparam name="T">The disposable's type.</typeparam>
    /// <param name="disposable">The subscription or other disposable to hold.</param>
    /// <param name="node">The node whose lifetime the disposable follows.</param>
    /// <returns><paramref name="disposable"/>, for chaining.</returns>
    /// <remarks>
    /// The disposable follows the node's lifetime, not its tree membership: removing or reparenting
    /// the node keeps it alive, and <c>Free</c> or <c>QueueFree</c> disposes it. If the node is
    /// already freed, the disposable is disposed immediately. Call this from the main thread.
    /// <para>
    /// While <paramref name="node"/> is propagating enter-tree, ready or exit-tree notifications
    /// to its children (such as when a child's <c>_Ready</c> calls this on its parent), Godot
    /// refuses to add children to it, so the internal holder is attached at the end of the frame
    /// instead. If the node is freed before then, the disposable is disposed at that point.
    /// </para>
    /// </remarks>
    public static T AddTo<T>(this T disposable, Node node)
        where T : IDisposable
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            disposable.Dispose();
            return disposable;
        }

        DisposeOnFreeNode.GetOrAttach(node).Add(disposable);
        return disposable;
    }
}
