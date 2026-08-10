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
/// The ENTERING page's motion is animator-based (<see cref="OnCreateAnimator"/>): the supported
/// seekable path, and immune to the managed-peer loss that breaks managed Transition subclasses.
/// The OUTGOING page never animates here — the presenter keeps its fragment ADDED and animates
/// its view in place (see <c>ScaffoldPresenter.StartLeavingPageAsync</c>), because
/// <see cref="AndroidX.Fragment.App.FragmentContainerView"/> deliberately draws exiting fragment
/// views BELOW the entering one, and a fragment being removed loses its view the instant the
/// transaction executes unless an exit animation holds it.
/// Shared elements (§8) fly in the presenter's overlay engine
/// (<see cref="ScaffoldSharedElementTransitions"/>): the presenter captures the source side
/// before the commit and hooks <see cref="OnFirstPreDraw"/> so the flights start at this
/// fragment's first pre-draw — the destination geometry exists by then (gate #1) and it is
/// the same frame this page becomes visible, making the live-view handoff seamless.
/// </summary>
internal sealed class ScaffoldPageFragment : Fragment
{
    private readonly TaskCompletionSource _presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IMauiContext? _mauiContext;
    private readonly Page? _page;
    private readonly ScaffoldPresentationHint _hint;
    private readonly AView? _container;
    private readonly ScaffoldPageTransition _transition = ScaffoldPageTransition.None;

    public ScaffoldPageFragment(
        IMauiContext mauiContext,
        Page page,
        ScaffoldPresentationHint hint,
        AView container,
        ScaffoldPageTransition transition)
    {
        _mauiContext = mauiContext;
        _page = page;
        _hint = hint;
        _container = container;
        _transition = transition;
    }

    /// <summary>
    /// Never called by the scaffold — insurance against fragment RESTORATION, which re-creates
    /// every saved fragment reflectively (<c>getDeclaredConstructor().newInstance()</c>, before
    /// any of our code runs) and throws <c>Fragment.InstantiationException</c> without a no-arg
    /// constructor. MAUI opts out of it by default: <c>PlatformMauiAppCompatActivity.onCreate</c>
    /// strips <c>android:support:fragments</c> from the bundle before AppCompat reads it (its own
    /// Shell fragments would not survive it either), so this only comes into play when an app
    /// overrides <c>MauiAppCompatActivity.AllowFragmentRestore</c> or hosts the scaffold in its
    /// own Activity.
    /// A restored instance carries none of the state that makes a page fragment work, and there
    /// is nothing to rehydrate it from — the scaffold rebuilds its fragments from the navigation
    /// model — so it unmounts itself on sight (see <see cref="OnCreate"/>).
    /// </summary>
    public ScaffoldPageFragment()
    {
    }

    /// <summary>Completes when the fragment's view is laid out and its enter animation (if any) finished.</summary>
    public Task PresentedTask => _presented.Task;

    /// <summary>
    /// Invoked at this fragment's first pre-draw — the traversal where its view is laid out and
    /// about to render. The presenter uses it to start shared-element flights (gate #1: the
    /// destination geometry exists exactly then).
    /// </summary>
    public Action? OnFirstPreDraw { get; set; }

    /// <summary>
    /// Whether this fragment's entry is animated by OUR animator (its presented signal comes from
    /// the animator end).
    /// A pop entry (the revealed page) animates only when the spec declares a Behind motion;
    /// a cross-area fade reveals this page by fading the outgoing one out ON TOP of it, so the
    /// entry itself stays at rest.
    /// </summary>
    private bool HasAnimatedEnter => _transition.IsAnimated
        && _hint switch
        {
            ScaffoldPresentationHint.Push => !_transition.Enter.IsIdentity,
            ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd => true,
            ScaffoldPresentationHint.Pop => !_transition.Behind.IsIdentity,
            _ => false
        };

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (_page is not null)
        {
            return;
        }

        // Restored husk (see the parameterless constructor): it hosts nothing and its container
        // died with the old activity — drop it before it reaches the container of the new one.
        _presented.TrySetResult();

        if (!ParentFragmentManager.IsDestroyed)
        {
            ParentFragmentManager
                .BeginTransaction()
                .SetReorderingAllowed(true)
                .Remove(this)
                .CommitAllowingStateLoss();
        }
    }

    public override AView OnCreateView(LayoutInflater inflater, ViewGroup? parent, Bundle? savedInstanceState)
    {
        if (_page is null || _mauiContext is null)
        {
            // Restored husk: an empty view keeps the state machine happy until OnCreate's
            // removal transaction runs.
            return new AView(RequireContext());
        }

        var page = _page;
        var platformView = page.ToPlatform(_mauiContext);
        (platformView.Parent as ViewGroup)?.RemoveView(platformView);

        // A remounted page still carries the motion its LEAVING animation left behind (covered
        // pages are detached, never destroyed, and the presenter deliberately leaves the
        // transform alone so unmounting does not flash it) — clear it before hosting it again.
        platformView.TranslationX = 0f;
        platformView.TranslationY = 0f;
        platformView.ScaleX = 1f;
        platformView.ScaleY = 1f;
        platformView.Alpha = 1f;
        ScaffoldPageDepth.ClearShadow(platformView);
        ScaffoldPageDepth.SetDim(platformView, 0f);

        platformView.LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        // The fragment's view is a HOST, never the page's platform view itself. A fragment's
        // removal takes its view out of "whatever parent it has" (SpecialEffectsController), and
        // the FragmentManager may only get to it after a navigation landing mid-transition has
        // re-mounted that page — the page's view is SHARED between the two fragments, so the
        // stale removal would rip the live page out of its new host and leave it detached for
        // good. Removing an emptied host instead is harmless.
        var host = new Android.Widget.FrameLayout(RequireContext())
                   {
                       LayoutParameters = new ViewGroup.LayoutParams(
                           ViewGroup.LayoutParams.MatchParent,
                           ViewGroup.LayoutParams.MatchParent)
                   };

        host.AddView(platformView);

        return host;
    }

    /// <summary>
    /// Invoked with the page's platform view as soon as it exists, BEFORE its first layout pass:
    /// the presenter uses it to hand the page the chrome-rewritten window insets it would
    /// otherwise miss (nothing re-dispatches them to a view mounted between window events).
    /// </summary>
    public Action<AView>? OnViewMounted { get; set; }

    public override void OnViewCreated(AView view, Bundle? savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        OnViewMounted?.Invoke(view);

        // Presented = first draw when no enter animation runs; the animator end wins otherwise.
        OneShotPreDrawListener.Add(view, new Java.Lang.Runnable(() =>
        {
            // One-shot, and CLEARED once it fires: the callback closes over the transition it
            // starts, which holds the page on its way OUT. A mounted fragment that keeps the
            // closure keeps that page — and its model — alive for as long as it is displayed.
            var onFirstPreDraw = OnFirstPreDraw;
            OnFirstPreDraw = null;
            onFirstPreDraw?.Invoke();

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
    }

    public override Animator? OnCreateAnimator(int transit, bool enter, int nextAnim)
    {
        // Exits are the presenter's business (the outgoing page left this container before the
        // transaction ran): only the entering page animates here.
        if (!enter)
        {
            return null;
        }

        var view = View;
        var width = _container?.Width ?? 0;

        if (view is null || width <= 0 || !HasAnimatedEnter)
        {
            CompleteEnter();

            return null;
        }

        if (_hint is ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd)
        {
            // Tab/root switch within an area: the new page slides in from the direction of
            // travel (spec-independent). Logical Start/End mapped LTR for now.
            var fromX = _hint == ScaffoldPresentationHint.SlideEnd ? width : -width;

            // Same duration as the outgoing page's slide (the presenter plays it from the SAME
            // spec): the two halves of a root switch must travel in lockstep.
            return BuildAnimator(view, fromX: fromX, toX: 0, _transition, _presented);
        }

        var identity = new ScaffoldTransitionMotion();

        // Push: the pushed page enters from the spec's Enter state.
        // Pop: the revealed page returns from the popped page's Behind state.
        var from = _hint == ScaffoldPresentationHint.Pop ? _transition.Behind : _transition.Enter;

        return BuildMotionAnimator(view, from, identity, _transition, _presented);
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
        TaskCompletionSource completion)
    {
        var width = _container?.Width ?? 0;
        var height = _container?.Height ?? 0;

        var animator = ObjectAnimator.OfPropertyValuesHolder(
            view,
            PropertyValuesHolder.OfFloat("translationX", (float)(from.FractionX * width), (float)(to.FractionX * width))!,
            PropertyValuesHolder.OfFloat("translationY", (float)(from.FractionY * height), (float)(to.FractionY * height))!,
            PropertyValuesHolder.OfFloat("scaleX", (float)from.Scale, (float)to.Scale)!,
            PropertyValuesHolder.OfFloat("scaleY", (float)from.Scale, (float)to.Scale)!,
            PropertyValuesHolder.OfFloat("alpha", (float)from.Opacity, (float)to.Opacity)!
        );

        animator.SetDuration((long)(spec.DurationSeconds * 1000));
        animator.AnimationEnd += (_, _) => completion.TrySetResult();

        return animator;
    }

    private void CompleteEnter()
    {
        // No enter animation possible: complete what the animator end would have signaled
        // (the no-animation case completes at first draw via the OnViewCreated hook).
        if (HasAnimatedEnter)
        {
            _presented.TrySetResult();
        }
    }

    private static ObjectAnimator BuildAnimator(AView view, float fromX, float toX, ScaffoldPageTransition spec, TaskCompletionSource completion)
    {
        var animator = ObjectAnimator.OfFloat(view, "translationX", fromX, toX)!;
        animator.SetDuration((long) (spec.DurationSeconds * 1000));
        animator.AnimationEnd += (_, _) => completion.TrySetResult();

        return animator;
    }
}
