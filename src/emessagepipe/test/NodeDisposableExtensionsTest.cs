using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Covers <see cref="NodeDisposableExtensions.AddTo{T}"/> — disposing subscriptions when the node
/// they belong to is freed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NodeDisposableExtensionsTest
{
    static Window SceneRoot => ((SceneTree)Engine.GetMainLoop()).Root;

    /// <summary>Freeing the node disposes everything added to it, once.</summary>
    [TestCase]
    public void AddTo_DisposesWhenNodeIsFreed()
    {
        var node = new Node();
        var first = new CountingDisposable().AddTo(node);
        var second = new CountingDisposable().AddTo(node);

        AssertThat(first.DisposeCount).IsEqual(0);

        node.Free();

        AssertThat(first.DisposeCount).IsEqual(1);
        AssertThat(second.DisposeCount).IsEqual(1);
    }

    /// <summary><c>QueueFree</c> disposes at the end of the frame, like it frees the node.</summary>
    [TestCase]
    public async Task AddTo_DisposesWhenNodeIsQueuedForDeletion()
    {
        var node = new Node();
        var disposable = new CountingDisposable().AddTo(node);

        node.QueueFree();
        await GodotFrames.WaitUntil(() => !GodotObject.IsInstanceValid(node));

        AssertThat(disposable.DisposeCount).IsEqual(1);
    }

    /// <summary>The disposable follows the node's lifetime, not its tree membership.</summary>
    [TestCase]
    public void AddTo_KeepsDisposableAliveWhenNodeIsReparented()
    {
        var first = AutoFree(new Node())!;
        var second = AutoFree(new Node())!;
        var node = new Node();
        first.AddChild(node);
        var disposable = new CountingDisposable().AddTo(node);

        node.Reparent(second);

        AssertThat(disposable.DisposeCount).IsEqual(0);
    }

    /// <summary>
    /// All disposables of a node share one internal holder, which stays out of the node's
    /// regular children.
    /// </summary>
    [TestCase]
    public void AddTo_AttachesOneInternalHolderPerNode()
    {
        var node = AutoFree(new Node())!;

        new CountingDisposable().AddTo(node);
        new CountingDisposable().AddTo(node);

        AssertThat(node.GetChildCount()).IsEqual(0);
        AssertThat(node.GetChildCount(true)).IsEqual(1);
    }

    /// <summary>
    /// While a node propagates ready to its children it cannot take a new child, so a child's
    /// <c>_Ready</c> adding to its parent has its holder attached at the end of the frame.
    /// </summary>
    [TestCase]
    public async Task AddTo_AttachesLaterWhenTheParentIsSettingUpItsChildren()
    {
        var parent = new Node();
        var child = new Node();
        parent.AddChild(child);
        var disposable = new CountingDisposable();
        child.Ready += () => disposable.AddTo(parent);
        var second = new CountingDisposable();
        child.Ready += () => second.AddTo(parent);

        SceneRoot.AddChild(parent);
        await GodotFrames.WaitUntil(() => parent.GetChildCount(true) == 2);

        AssertThat(parent.GetChildCount(true)).IsEqual(2);
        AssertThat(parent.GetChildCount()).IsEqual(1);
        AssertThat(disposable.DisposeCount).IsEqual(0);

        parent.Free();

        AssertThat(disposable.DisposeCount).IsEqual(1);
        AssertThat(second.DisposeCount).IsEqual(1);
    }

    /// <summary>
    /// Exit-tree propagation cannot be detected up front, so there the refused <c>AddChild</c> is
    /// noticed afterwards and the holder is attached at the end of the frame as well.
    /// </summary>
    [TestCase]
    public async Task AddTo_AttachesLaterWhenTheParentIsRemovingItsChildren()
    {
        var parent = AutoFree(new Node())!;
        var child = new Node();
        parent.AddChild(child);
        SceneRoot.AddChild(parent);
        var disposable = new CountingDisposable();
        child.TreeExiting += () => disposable.AddTo(parent);

        SceneRoot.RemoveChild(parent);
        await GodotFrames.WaitUntil(() => parent.GetChildCount(true) == 2);

        AssertThat(parent.GetChildCount(true)).IsEqual(2);
        AssertThat(disposable.DisposeCount).IsEqual(0);
    }

    /// <summary>A node freed before the deferred attach still has its disposables disposed.</summary>
    [TestCase]
    public async Task AddTo_DisposesWhenTheParentIsFreedBeforeTheDeferredAttach()
    {
        var parent = new Node();
        var child = new Node();
        parent.AddChild(child);
        var disposable = new CountingDisposable();
        child.Ready += () => disposable.AddTo(parent);

        SceneRoot.AddChild(parent);
        parent.Free();
        await GodotFrames.WaitUntil(() => disposable.DisposeCount > 0);

        AssertThat(disposable.DisposeCount).IsEqual(1);
    }

    /// <summary>Adding to a node that is already freed disposes straight away.</summary>
    [TestCase]
    public void AddTo_DisposesImmediatelyWhenNodeIsAlreadyFreed()
    {
        var node = new Node();
        node.Free();

        var disposable = new CountingDisposable().AddTo(node);

        AssertThat(disposable.DisposeCount).IsEqual(1);
    }
}
