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
    /// <summary>Holders still waiting for their deferred attach, by the instance id of their node.</summary>
    static readonly Dictionary<ulong, DisposeOnFreeNode> PendingByNodeId = new();

    readonly List<IDisposable> disposables = new();
    bool freed;

    /// <summary>Returns the holder attached to <paramref name="node"/>, attaching one first if needed.</summary>
    /// <remarks>
    /// Godot refuses to add children to a node while it propagates enter-tree, ready or exit-tree
    /// notifications to its children, such as when a child's <c>_Ready</c> subscribes on its
    /// parent. The holder is then attached at the end of the frame instead; until that happens it
    /// already collects disposables, and if the node is freed first it disposes them right then.
    /// </remarks>
    internal static DisposeOnFreeNode GetOrAttach(Node node)
    {
        for (var i = node.GetChildCount(true) - 1; i >= 0; i--)
        {
            if (node.GetChild(i, true) is DisposeOnFreeNode existing)
            {
                return existing;
            }
        }

        var nodeId = node.GetInstanceId();
        if (PendingByNodeId.TryGetValue(nodeId, out var pending))
        {
            return pending;
        }

        var holder = new DisposeOnFreeNode { Name = nameof(DisposeOnFreeNode) };

        // A node in the tree that is not ready yet is propagating ready to its children, so
        // adding now would only make Godot print an add_child error. Exit-tree propagation cannot
        // be detected up front, so that case is caught by checking the parent afterwards.
        var mayBeBusy = node.IsInsideTree() && !node.IsNodeReady();
        if (!mayBeBusy && TryAttach(node, holder))
        {
            return holder;
        }

        PendingByNodeId[nodeId] = holder;
        Callable.From(() => holder.AttachDeferred(node, nodeId)).CallDeferred();
        return holder;
    }

    static bool TryAttach(Node node, DisposeOnFreeNode holder)
    {
        node.AddChild(holder, false, InternalMode.Back);
        return holder.GetParent() == node;
    }

    void AttachDeferred(Node node, ulong nodeId)
    {
        PendingByNodeId.Remove(nodeId);

        if (!IsInstanceValid(node))
        {
            // The node was freed before the holder could follow it: dispose as it would have.
            Free();
            return;
        }

        if (!TryAttach(node, this))
        {
            // Deferred calls run after notification propagation, so this is not the busy case
            // anymore. Dispose rather than hold the disposables in a holder nothing ever frees.
            GD.PushError($"{nameof(NodeDisposableExtensions.AddTo)} could not attach to {node.Name}; disposing now.");
            Free();
        }
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
