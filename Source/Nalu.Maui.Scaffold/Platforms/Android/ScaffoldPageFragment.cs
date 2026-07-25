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
/// Page transitions are animator-based (<see cref="OnCreateAnimator"/>): the supported seekable
/// path, and immune to the managed-peer loss that breaks managed Transition subclasses.
/// Shared elements (§8, PoC spike B) ride on the native androidx transition framework:
/// <c>transitionName</c>s are stamped on tagged views, and when the presenter wires
/// <c>AddSharedElement</c> + <c>SharedElementEnterTransition</c> the enter transition is
/// POSTPONED until the first pre-draw so the end geometry exists (gate #1).
/// </summary>
internal sealed class ScaffoldPageFragment(IMauiContext mauiContext, Page page, ScaffoldPresentationHint hint, AView container, bool postponeForSharedElements = false) : Fragment
{
    private const long _transitionDurationMs = 250;

    private readonly TaskCompletionSource _presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the fragment's view is laid out and its enter animation (if any) finished.</summary>
    public Task PresentedTask => _presented.Task;

    /// <summary>Completes when the fragment's exit animation finished (or immediately when it has none).</summary>
    public Task DismissedTask => _dismissed.Task;

    /// <summary>Whether this fragment's entry is animated (its presented signal comes from the animator end).</summary>
    private bool HasAnimatedEnter => hint is ScaffoldPresentationHint.Push or ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd;

    public override AView OnCreateView(LayoutInflater inflater, ViewGroup? parent, Bundle? savedInstanceState)
    {
        var platformView = page.ToPlatform(mauiContext);
        (platformView.Parent as ViewGroup)?.RemoveView(platformView);

        // A remounted page keeps whatever translation its last exit animation left behind
        // (covered pages are detached, never destroyed) — reset before hosting it again.
        platformView.TranslationX = 0f;
        platformView.TranslationZ = 0f;

        platformView.LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        return platformView;
    }

    public override void OnViewCreated(AView view, Bundle? savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        // Stamp android:transitionName on every tagged view — the native SET matches by name.
        foreach (var (name, taggedView) in ScaffoldTransitions.Collect(page))
        {
            ViewCompat.SetTransitionName(taggedView.ToPlatform(mauiContext), name);
        }

        if (postponeForSharedElements)
        {
            // Incoming-readiness gate: hold the shared-element transition until the first
            // layout/draw pass so the end geometry exists.
            PostponeEnterTransition();
        }

        // Presented = first draw when no enter animation runs; the animator end wins otherwise.
        OneShotPreDrawListener.Add(view, new Java.Lang.Runnable(() =>
        {
            if (postponeForSharedElements)
            {
                StartPostponedEnterTransition();
            }

            if (!HasAnimatedEnter)
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

        if (enter && hint is ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd)
        {
            // Tab/root switch: the new page slides in over the old one in the direction of
            // travel. Logical Start/End mapped LTR for now (RTL mapping arrives with the engine).
            var fromX = hint == ScaffoldPresentationHint.SlideEnd ? width : -width;

            return BuildAnimator(view, fromX: fromX, toX: 0, elevate: true, _presented);
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
            // No enter animation possible: complete what the animator end would have signaled
            // (the no-animation case completes at first draw via the OnViewCreated hook).
            if (HasAnimatedEnter)
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
