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
/// Shared elements (§8) fly in the presenter's overlay engine
/// (<see cref="ScaffoldSharedElementTransitions"/>): the presenter captures the source side
/// before the commit and hooks <see cref="OnFirstPreDraw"/> so the flights start at this
/// fragment's first pre-draw — the destination geometry exists by then (gate #1) and it is
/// the same frame this page becomes visible, making the live-view handoff seamless.
/// </summary>
internal sealed class ScaffoldPageFragment(
    IMauiContext mauiContext,
    Page page,
    ScaffoldPresentationHint hint,
    AView container,
    ScaffoldPageTransition transition) : Fragment
{
    private const long _transitionDurationMs = 250;

    private readonly TaskCompletionSource _presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ScaffoldPresentationHint _removalHint = ScaffoldPresentationHint.None;
    private ScaffoldPageTransition _removalTransition = ScaffoldPageTransition.Default;

    /// <summary>Completes when the fragment's view is laid out and its enter animation (if any) finished.</summary>
    public Task PresentedTask => _presented.Task;

    /// <summary>Completes when the fragment's exit animation finished (or immediately when it has none).</summary>
    public Task DismissedTask => _dismissed.Task;

    /// <summary>
    /// Invoked at this fragment's first pre-draw — the traversal where its view is laid out and
    /// about to render. The presenter uses it to start shared-element flights (gate #1: the
    /// destination geometry exists exactly then).
    /// </summary>
    public Action? OnFirstPreDraw { get; set; }

    /// <summary>
    /// Whether this fragment's entry is animated by OUR animator (its presented signal comes from
    /// the animator end).
    /// A pop entry (the revealed page) animates only when the spec declares a Behind motion.
    /// </summary>
    private bool HasAnimatedEnter => transition.IsAnimated
        && hint switch
        {
            ScaffoldPresentationHint.Push => !transition.Enter.IsIdentity,
            ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd => true,
            ScaffoldPresentationHint.Pop => !transition.Behind.IsIdentity,
            _ => false
        };

    /// <summary>
    /// Called by the presenter right before this fragment is replaced: the CURRENT navigation's
    /// hint decides the exit choreography (a pop replays the spec's Enter motion in reverse; a
    /// push plays the incoming page's Behind motion) — the creation hint says nothing about how
    /// we leave.
    /// </summary>
    public void PrepareRemoval(ScaffoldPresentationHint removalHint, ScaffoldPageTransition removalTransition)
    {
        _removalHint = removalHint;
        _removalTransition = removalTransition;
    }

    public override AView OnCreateView(LayoutInflater inflater, ViewGroup? parent, Bundle? savedInstanceState)
    {
        var platformView = page.ToPlatform(mauiContext);
        (platformView.Parent as ViewGroup)?.RemoveView(platformView);

        // A remounted page keeps whatever motion state its last exit animation left behind
        // (covered pages are detached, never destroyed) — reset before hosting it again.
        platformView.TranslationX = 0f;
        platformView.TranslationY = 0f;
        platformView.TranslationZ = 0f;
        platformView.ScaleX = 1f;
        platformView.ScaleY = 1f;
        platformView.Alpha = 1f;

        // A page whose shared elements took off in a previous SET may still carry their hidden
        // render state (see ScaffoldPageRestore) — no-op unless a capture is pending.
        ScaffoldPageRestore.Repair(page);

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
            OnFirstPreDraw?.Invoke();

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

        var identity = new ScaffoldTransitionMotion();

        if (enter && hint == ScaffoldPresentationHint.Push && HasAnimatedEnter)
        {
            // The pushed page enters from the spec's Enter state.
            return BuildMotionAnimator(view, transition.Enter, identity, transition, elevate: true, _presented);
        }

        if (enter && hint is ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd)
        {
            // Tab/root switch: the new page slides in over the old one in the direction of
            // travel (spec-independent). Logical Start/End mapped LTR for now.
            var fromX = hint == ScaffoldPresentationHint.SlideEnd ? width : -width;

            return BuildAnimator(view, fromX: fromX, toX: 0, elevate: true, _presented);
        }

        if (enter && hint == ScaffoldPresentationHint.Pop && HasAnimatedEnter)
        {
            // The revealed page returns from the popped page's Behind state.
            return BuildMotionAnimator(view, transition.Behind, identity, transition, elevate: false, _presented);
        }

        if (!enter && IsRemoving && _removalHint == ScaffoldPresentationHint.Pop && _removalTransition.IsAnimated)
        {
            // Popped: replay this page's Enter motion in reverse, above the revealed page.
            return BuildMotionAnimator(view, identity, _removalTransition.Enter, _removalTransition, elevate: true, _dismissed);
        }

        if (!enter && IsRemoving && _removalHint == ScaffoldPresentationHint.Push && _removalTransition.IsAnimated && !_removalTransition.Behind.IsIdentity)
        {
            // Covered: play the INCOMING page's Behind motion beneath its entry.
            return BuildMotionAnimator(view, identity, _removalTransition.Behind, _removalTransition, elevate: false, _dismissed);
        }

        CompleteFor(enter);

        return null;
    }

    /// <summary>
    /// Property animator between two §8.2 motion states (fractional translation, scale about
    /// center, opacity). A single ObjectAnimator with PropertyValuesHolders — the supported,
    /// seekable fragment-animation path.
    /// </summary>
    private Animator BuildMotionAnimator(
        AView view,
        ScaffoldTransitionMotion from,
        ScaffoldTransitionMotion to,
        ScaffoldPageTransition spec,
        bool elevate,
        TaskCompletionSource completion)
    {
        var width = container.Width;
        var height = container.Height;

        if (elevate)
        {
            view.TranslationZ = 1f;
        }

        var animator = ObjectAnimator.OfPropertyValuesHolder(
            view,
            PropertyValuesHolder.OfFloat("translationX", (float)(from.FractionX * width), (float)(to.FractionX * width))!,
            PropertyValuesHolder.OfFloat("translationY", (float)(from.FractionY * height), (float)(to.FractionY * height))!,
            PropertyValuesHolder.OfFloat("scaleX", (float)from.Scale, (float)to.Scale)!,
            PropertyValuesHolder.OfFloat("scaleY", (float)from.Scale, (float)to.Scale)!,
            PropertyValuesHolder.OfFloat("alpha", (float)from.Opacity, (float)to.Opacity)!
        )!;

        animator.SetDuration((long)(spec.DurationSeconds * 1000));
        animator.AnimationEnd += (_, _) =>
        {
            view.TranslationZ = 0f;
            completion.TrySetResult();
        };

        return animator;
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
