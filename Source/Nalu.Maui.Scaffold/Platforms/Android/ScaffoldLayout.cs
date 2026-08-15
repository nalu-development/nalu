using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Platform root of a scaffold-hosted app: a plain FrameLayout — match-parent children
/// (the presenter's page layer, chrome layers, overlays) are measured and laid out natively.
/// Re-dispatches window insets to the page layer whenever the tab bar strip's height changes,
/// so the §5.4 inset rewrite always reflects the current chrome footprint.
/// </summary>
public sealed class ScaffoldLayout : FrameLayout
{
    /// <summary>The layer hosting page content (its insets are rewritten per §5.4).</summary>
    internal AView? PageLayer { get; set; }

    /// <summary>The bottom chrome strip (tab bar), when mounted.</summary>
    internal AView? TabBarLayer { get; set; }

    /// <summary>The top chrome strip (nav bar), when mounted.</summary>
    internal AView? NavBarLayer { get; set; }

    /// <summary>
    /// The bottom inset (px) the page layer rewrites into the system-bars insets. Deliberately
    /// DECOUPLED from the strip's visual state: bar hide/show animations never relayout the
    /// outgoing page — the presenter sets the target value BEFORE the fragment transaction and
    /// the incoming page attaches with its final insets.
    /// </summary>
    internal int PageBottomInsetPx { get; set; }

    /// <summary>The top inset (px) the page layer rewrites into the system-bars insets (see <see cref="PageBottomInsetPx"/>).</summary>
    internal int PageTopInsetPx { get; set; }

    /// <summary>Whether the presenter wants the bottom chrome footprint applied once the strip is measured.</summary>
    internal bool ChromeBottomDesired { get; set; }

    /// <summary>Whether the presenter wants the top chrome footprint applied once the strip is measured.</summary>
    internal bool ChromeTopDesired { get; set; }

    /// <summary>The current page's soft-keyboard policy (resolved by the presenter, read live).</summary>
    internal Func<ScaffoldKeyboardMode>? PageKeyboardMode { get; set; }

    /// <summary>
    /// Whether a presented sheet/popup OWNS the keyboard (set by the presenter). The keyboard inset
    /// goes to ONE surface: the topmost sheet or popup when one is presented, the page otherwise.
    /// </summary>
    internal Func<bool>? OverlayOwnsKeyboard { get; set; }

    /// <summary>The keyboard overlap (px) the PAGE reacts to: 0 while an overlay owns the keyboard.</summary>
    internal int PageKeyboardOverlapPx => OverlayOwnsKeyboard?.Invoke() == true ? 0 : ImeBottomInsetPx;

    /// <summary>
    /// Full height (px) of the bottom chrome strip: bar footprint + system bottom inset.
    /// Zero when no tab bar is shown.
    /// </summary>
    internal int ChromeBottomFootprint
        => TabBarLayer is { Visibility: Android.Views.ViewStates.Visible } tabBar ? tabBar.Height : 0;

    /// <summary>Activation constructor.</summary>
    public ScaffoldLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <summary>Initializes a new <see cref="ScaffoldLayout"/>.</summary>
    public ScaffoldLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);
    }

    private ImeAnimationCallback? _imeAnimationCallback;
    private AView? _imeAnimationHost;
    private FocusChangeListener? _focusChangeListener;

    /// <summary>
    /// A Pan-mode surface follows the FOCUSED input, and focus can move without the IME moving
    /// (tab to the next field): window focus changes re-raise <see cref="KeyboardInsetsChanged"/>.
    /// </summary>
    private sealed class FocusChangeListener(ScaffoldLayout owner) : Java.Lang.Object, ViewTreeObserver.IOnGlobalFocusChangeListener
    {
        public void OnGlobalFocusChanged(AView? oldFocus, AView? newFocus)
        {
            if (owner.ImeBottomInsetPx > 0)
            {
                (owner.PageLayer as ScaffoldPageLayerLayout)?.ApplyKeyboard();
                owner.KeyboardInsetsChanged?.Invoke();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();

        // Per-frame IME tracking: while the keyboard animates, the running IME insets are
        // delivered through the window-insets ANIMATION callback, and overlays re-placed against
        // them travel with the keyboard. The callback must sit on the DECOR view: MAUI's own
        // callback on the window's root CoordinatorLayout is DISPATCH_MODE_STOP, so nothing
        // below it (this layout included) ever hears an animation frame. CONTINUE_ON_SUBTREE
        // leaves MAUI's dispatch exactly as it was.
        if (RootView is { } decor && !ReferenceEquals(decor, this))
        {
            _imeAnimationCallback ??= new ImeAnimationCallback(this);
            _imeAnimationHost = decor;
            ViewCompat.SetWindowInsetsAnimationCallback(decor, _imeAnimationCallback);
        }

        if (ViewTreeObserver is { IsAlive: true } observer)
        {
            _focusChangeListener ??= new FocusChangeListener(this);
            observer.AddOnGlobalFocusChangeListener(_focusChangeListener);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromWindow()
    {
        if (_imeAnimationHost is { } host)
        {
            ViewCompat.SetWindowInsetsAnimationCallback(host, null);
            _imeAnimationHost = null;
        }

        if (_focusChangeListener is { } focusListener && ViewTreeObserver is { IsAlive: true } observer)
        {
            observer.RemoveOnGlobalFocusChangeListener(focusListener);
        }

        _imeAnimating = false;
        base.OnDetachedFromWindow();
    }

    /// <summary>
    /// The soft keyboard's overlap with this layout (px), measured from its bottom edge; 0 while
    /// hidden. Read from the IME window insets — the scaffold-hosted activity is edge-to-edge under
    /// <c>adjustResize</c> (see <c>ScaffoldKeyboardSupport</c>), so the keyboard is an inset, not
    /// a window resize. During the keyboard animation this follows the running value.
    /// </summary>
    internal int ImeBottomInsetPx { get; private set; }

    /// <summary>
    /// Raised when <see cref="ImeBottomInsetPx"/> changed — at the insets dispatch that carries the
    /// keyboard's final state and, while it animates, on every animation frame. Overlays whose
    /// geometry depends on the keyboard (bottom sheets, popups) re-place themselves.
    /// </summary>
    internal Action? KeyboardInsetsChanged { get; set; }

    private bool _imeAnimating;

    private void UpdateImeInset(WindowInsetsCompat? insets)
    {
        if (insets is null)
        {
            return;
        }

        var ime = insets.GetInsets(WindowInsetsCompat.Type.Ime());
        var value = insets.IsVisible(WindowInsetsCompat.Type.Ime()) || _imeAnimating ? ime?.Bottom ?? 0 : 0;

        if (value != ImeBottomInsetPx)
        {
            ImeBottomInsetPx = value;
            (PageLayer as ScaffoldPageLayerLayout)?.ApplyKeyboard();
            KeyboardInsetsChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
    {
        // The dispatch preceding an IME animation carries the keyboard's END state; while an
        // animation runs, the frames come from the animation callback instead (the dispatched
        // value would jump ahead of the moving keyboard).
        if (!_imeAnimating && insets is not null)
        {
            UpdateImeInset(WindowInsetsCompat.ToWindowInsetsCompat(insets, this));
        }

        return base.DispatchApplyWindowInsets(insets);
    }

    private sealed class ImeAnimationCallback(ScaffoldLayout owner) : WindowInsetsAnimationCompat.Callback(DispatchModeContinueOnSubtree)
    {
        private static bool IsIme(WindowInsetsAnimationCompat? animation)
            => animation is not null && (animation.TypeMask & WindowInsetsCompat.Type.Ime()) != 0;

        public override void OnPrepare(WindowInsetsAnimationCompat? animation)
        {
            base.OnPrepare(animation);

            if (IsIme(animation))
            {
                owner._imeAnimating = true;
            }
        }

        public override WindowInsetsCompat? OnProgress(WindowInsetsCompat? insets, IList<WindowInsetsAnimationCompat>? runningAnimations)
        {
            if (owner._imeAnimating && runningAnimations is not null && runningAnimations.Any(IsIme))
            {
                owner.UpdateImeInset(insets);
            }

            return insets;
        }

        public override void OnEnd(WindowInsetsAnimationCompat? animation)
        {
            base.OnEnd(animation);

            if (IsIme(animation))
            {
                owner._imeAnimating = false;
                owner.UpdateImeInset(ViewCompat.GetRootWindowInsets(owner));
            }
        }
    }

    /// <summary>
    /// Raised when the container changed SHAPE (rotation, split view, multi-window resize).
    /// Presented overlays compute their geometry from the window of the moment they were shown,
    /// so they need to hear about it.
    /// </summary>
    public Action? WindowGeometryChanged { get; set; }

    private int _lastWidth = -1;
    private int _lastHeight = -1;

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);

        var width = right - left;
        var height = bottom - top;

        if (width != _lastWidth || height != _lastHeight)
        {
            _lastWidth = width;
            _lastHeight = height;

            WindowGeometryChanged?.Invoke();
        }

        // The strips' heights are only known after layout: on the first (or a re-)measure while
        // the chrome is desired, publish the footprints and re-dispatch insets to the page layer
        // (mirrors NaluShellItemRendererOuterLayout).
        var insetsChanged = false;

        if (ChromeBottomDesired && ChromeBottomFootprint is > 0 and var footprint && PageBottomInsetPx != footprint)
        {
            PageBottomInsetPx = footprint;
            insetsChanged = true;
        }

        if (ChromeTopDesired
            && NavBarLayer is { Visibility: Android.Views.ViewStates.Visible, Height: > 0 } navBar
            && PageTopInsetPx != navBar.Height)
        {
            PageTopInsetPx = navBar.Height;
            insetsChanged = true;
        }

        if (insetsChanged && PageLayer is { } pageLayer)
        {
            ViewCompat.RequestApplyInsets(pageLayer);
        }
    }

}
