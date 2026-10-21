using System;
using System.Threading.Tasks;
using Godot;

namespace Enaweg.MessagePipe.Tests;

/// <summary>Helpers for tests that depend on Godot's main loop advancing.</summary>
static class GodotFrames
{
    /// <summary>Waits frame by frame until <paramref name="condition"/> holds, or the frames run out.</summary>
    public static async Task WaitUntil(Func<bool> condition, int maxFrames = 10)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (var frame = 0; frame < maxFrames && !condition(); frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Runs <paramref name="action"/> on the main thread through <c>CallDeferred</c>.</summary>
    public static Task RunOnMainThread(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Callable.From(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }).CallDeferred();
        return completion.Task;
    }
}
