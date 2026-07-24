using Android.Animation;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace Nalu;

/// <summary>
/// Hosts a MAUI page's platform view as fragment content (the architecture MAUI Shell itself
/// uses on Android, and the base for predictive-back integration later).
/// Transitions are animator-based (<see cref="OnCreateAnimator"/>): the supported seekable path,
/// and immune to the managed-peer loss that breaks managed Transition subclasses.
/// </summary>
internal sealed class ScaffoldPageFragment(IMauiContext mauiContext, Page page, ScaffoldPresentationHint hint, AView container) : Fragment
{
    private const long _transitionDurationMs = 250;

    private readonly TaskCompletionSource _presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the fragment's view is laid out and its enter animation (if any) finished.</summary>
    public Task PresentedTask => _presented.Task;

    /// <summary>Completes when the fragment's exit animation finished (or immediately when it has none).</summary>
    public Task DismissedTask => _dismissed.Task;

    public override AView OnCreateView(LayoutInflater inflater, ViewGroup? parent, Bundle? savedInstanceState)
    {
        var platformView = page.ToPlatform(mauiContext);
        (platformView.Parent as ViewGroup)?.RemoveView(platformView);
        platformView.LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        return platformView;
    }

    public override void OnViewCreated(AView view, Bundle? savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        // Presented = first draw when no enter animation runs; the animator end wins otherwise.
        OneShotPreDrawListener.Add(view, new Java.Lang.Runnable(() =>
        {
            if (hint != ScaffoldPresentationHint.Push)
            {
                _presented.TrySetResult();
            }
        }));
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        _presented.TrySetResult();
        _dismissed.TrySetResult();
    }

    public override Animator? OnCreateAnimator(int transit, bool enter, int nextAnim)
    {
        var view = View;
        var width = container.Width;

        if (view is null || width <= 0)
        {
            CompleteFor(enter);

            return null;
        }

        if (enter && hint == ScaffoldPresentationHint.Push)
        {
            return BuildAnimator(view, fromX: width, toX: 0, elevate: true, _presented);
        }

        if (!enter && IsRemoving && hint == ScaffoldPresentationHint.Pop)
        {
            return BuildAnimator(view, fromX: 0, toX: width, elevate: true, _dismissed);
        }

        CompleteFor(enter);

        return null;
    }

    private void CompleteFor(bool enter)
    {
        if (enter)
        {
            // No enter animation: presentation completes at first draw (OnViewCreated hook).
            if (hint == ScaffoldPresentationHint.Push)
            {
                _presented.TrySetResult();
            }
        }
        else
        {
            _dismissed.TrySetResult();
        }
    }

    private static ObjectAnimator BuildAnimator(AView view, float fromX, float toX, bool elevate, TaskCompletionSource completion)
    {
        if (elevate)
        {
            // The animating page must render above the static one regardless of fragment order.
            view.TranslationZ = 1f;
        }

        var animator = ObjectAnimator.OfFloat(view, "translationX", fromX, toX)!;
        animator.SetDuration(_transitionDurationMs);
        animator.AnimationEnd += (_, _) =>
        {
            view.TranslationZ = 0f;
            completion.TrySetResult();
        };

        return animator;
    }
}
