using Android.Views;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Frame-pipeline awaits for Android views: completes when the view has gone through its next
/// layout pass (the ViewTreeObserver global-layout callback that follows a traversal), so callers
/// sequence "after the view is laid out where it is" without timers. A detached view resolves on
/// its next attach + layout; a view whose window is gone resolves immediately.
/// </summary>
internal static class ScaffoldViewFrames
{
    /// <summary>Completes after the next layout pass of <paramref name="view"/>'s window (or immediately when it is not attached to one).</summary>
    public static Task NextLayoutAsync(AView view)
    {
        if (view.ViewTreeObserver is not { IsAlive: true } observer || !view.IsAttachedToWindow)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new OneShotGlobalLayoutListener(view, tcs);
        observer.AddOnGlobalLayoutListener(listener);

        // Make sure a traversal is coming even when nothing else asked for one.
        view.RequestLayout();

        return tcs.Task;
    }

    private sealed class OneShotGlobalLayoutListener(AView view, TaskCompletionSource tcs) : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    {
        public void OnGlobalLayout()
        {
            if (view.ViewTreeObserver is { IsAlive: true } observer)
            {
                observer.RemoveOnGlobalLayoutListener(this);
            }

            tcs.TrySetResult();
        }
    }
}
