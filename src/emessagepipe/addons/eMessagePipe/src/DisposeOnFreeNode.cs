using System;
using System.Collections.Generic;
using Godot;

namespace Enaweg.MessagePipe;

/// <summary>
/// Internal child that <see cref="NodeDisposableExtensions.AddTo{T}"/> attaches to a node. Godot
/// frees it together with that node, and it disposes everything it holds when that happens.
/// </summary>
/// <remarks>
/// It is added as an internal child, so <see cref="Node.GetChildren"/> and scene saving ignore it,
/// and it is not tied to tree membership, so reparenting the node keeps the disposables alive.
/// </remarks>
internal sealed partial class DisposeOnFreeNode : Node
{
    readonly List<IDisposable> disposables = new();
    bool freed;

    /// <summary>Returns the holder attached to <paramref name="node"/>, attaching one first if needed.</summary>
    internal static DisposeOnFreeNode GetOrAttach(Node node)
    {
        for (var i = node.GetChildCount(true) - 1; i >= 0; i--)
        {
            if (node.GetChild(i, true) is DisposeOnFreeNode existing)
            {
                return existing;
            }
        }

        var holder = new DisposeOnFreeNode { Name = nameof(DisposeOnFreeNode) };
        node.AddChild(holder, false, InternalMode.Back);
        return holder;
    }

    /// <summary>Holds <paramref name="disposable"/> until this node is freed.</summary>
    internal void Add(IDisposable disposable)
    {
        if (freed)
        {
            disposable.Dispose();
            return;
        }

        disposables.Add(disposable);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete)
        {
            return;
        }

        freed = true;
        foreach (var disposable in disposables)
        {
            // Throwing out of NOTIFICATION_PREDELETE would skip the remaining disposables.
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                GD.PushError(exception.ToString());
            }
        }

        disposables.Clear();
    }
}
