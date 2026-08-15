using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using Insets = AndroidX.Core.Graphics.Insets;

namespace Nalu;

/// <summary>Shared helpers for the chrome strip layouts.</summary>
internal static class ScaffoldChromeLayoutHelpers
{
    /// <summary>
    /// Disables child-clipping through the mounted bar subtree: MAUI shadows draw OUTSIDE
    /// their wrapper's bounds, and Android's ViewGroup default (<c>clipChildren=true</c>)
    /// truncates them at the first layout boundary (visible as a hard seam around the tab bar
    /// pill's shadow). MAUI's own clipping uses view-level ClipBounds and is unaffected.
    /// </summary>
    public static void DisableChildClipping(AView view)
    {
        if (view is not ViewGroup group)
        {
            return;
        }

        group.SetClipChildren(false);
        group.SetClipToPadding(false);

        for (var i = 0; i < group.ChildCount; i++)
        {
            if (group.GetChildAt(i) is { } child)
            {
                DisableChildClipping(child);
            }
        }
    }
}

/// <summary>
/// Hosts the page content (the presenter's fragment container) and rewrites the system-bars
/// insets before they propagate down (§5.4): while the tab bar strip is visible, the bottom
/// inset becomes the strip height (which already covers the system inset) — the page treats the
/// bar exactly like a system bar. Mirrors NaluShellItemRendererNavigationLayout.
/// </summary>
// ReSharper disable once RedundantNameQualifier — inside a View subclass the bare name binds to the nested Android.Views.View.IOnApplyWindowInsetsListener
internal sealed class ScaffoldPageLayerLayout : FrameLayout, AndroidX.Core.View.IOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();
    private static readonly int _imeInsetsType = WindowInsetsCompat.Type.Ime();

    /// <summary>The last insets the window dispatched, BEFORE the chrome rewrite (see <see cref="ApplyInsetsTo"/>).</summary>
    private WindowInsetsCompat? _lastRawInsets;

    /// <summary>
    /// The predictive-back peek and its OWN chrome intent. The layer-wide intent
    /// (<see cref="ScaffoldLayout.PageTopInsetPx"/>/<see cref="ScaffoldLayout.PageBottomInsetPx"/>)
    /// belongs to the scrubbed TOP page for the whole gesture, so a peeked page whose chrome
    /// differs (nav bar shown vs overlapped, tab bar back vs hidden) gets its rewrite
    /// re-dispatched directly to its subtree after every layer-level dispatch — it must be
    /// padded for where it will LAND, or the commit jumps.
    /// </summary>
    private (AView View, int TopInsetPx, int BottomInsetPx)? _peekIntent;

    public ScaffoldPageLayerLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public ScaffoldPageLayerLayout(Context context)
        : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);

        // The page subtree gets its OWN MAUI inset listener: the window root's is gated for the
        // whole IME animation, which would hold the keyboard fold below (per frame) until the
        // keyboard has stopped moving. See ScaffoldMauiInsetListenerBridge.
        ScaffoldMauiInsetListenerBridge.RegisterParent(this);
    }

    /// <summary>
    /// Applies the current page's soft-keyboard policy (<see cref="ScaffoldLayout.PageKeyboardMode"/>)
    /// against the running IME value (<see cref="ScaffoldLayout.ImeBottomInsetPx"/>) — called by
    /// the scaffold layout on every change, per animation frame:
    /// <see cref="ScaffoldKeyboardMode.Resize"/> re-dispatches the insets (the rewrite folds the
    /// keyboard into the bottom system inset, MAUI pads the page above it),
    /// <see cref="ScaffoldKeyboardMode.Pan"/> slides the whole layer by the least that keeps the
    /// focused input above the keyboard, <see cref="ScaffoldKeyboardMode.None"/> leaves the page alone.
    /// </summary>
    public void ApplyKeyboard()
    {
        if (Parent is not ScaffoldLayout scaffoldLayout || Context is not { } context)
        {
            return;
        }

        var mode = scaffoldLayout.PageKeyboardMode?.Invoke() ?? ScaffoldKeyboardMode.Resize;
        var overlap = context.FromPixels(scaffoldLayout.PageKeyboardOverlapPx);
        double pan = 0;

        if (mode == ScaffoldKeyboardMode.Pan && overlap > 0)
        {
            var keyboardTop = context.FromPixels(Height) - overlap;
            var focused = ScaffoldFocusedInput.BottomIn(this, context);
            var needed = focused is { } focusedBottom ? focusedBottom + ScaffoldOverlayGeometry.PanGap - keyboardTop : overlap;
            pan = Math.Clamp(needed, 0, overlap);
        }

        TranslationY = (float)-context.ToPixels(pan);

        if (mode == ScaffoldKeyboardMode.Resize && _lastRawInsets is { } raw)
        {
            ViewCompat.DispatchApplyWindowInsets(this, raw);
        }
    }

    /// <summary>
    /// Set by the presenter for the span of a page transition. It silences two things until the
    /// pages are back at rest.
    /// INPUT: both pages are mounted and hit-testable at their ANIMATED positions during those
    /// few hundred milliseconds, so a tap would otherwise reach a control on the page that is on
    /// its way out (visible beside the incoming one through a push, above it through a pop) or on
    /// one that has not landed yet.
    /// INSETS: MAUI recomputes a page's safe-area padding from its ON-SCREEN position, so a
    /// dispatch landing while a page is translated pads it for where it momentarily sits — and
    /// that padding survives at rest (a slide-up entry ends with its content under the nav bar).
    /// The chrome strips freeze against the same hazard; their own show/hide slides end with a
    /// re-dispatch, which is exactly what lands mid-page-transition.
    /// Clearing the flag re-dispatches, so the pages recompute where they finally are.
    /// </summary>
    public bool TransitionInFlight
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            if (!value)
            {
                ViewCompat.RequestApplyInsets(this);
            }
        }
    }

    /// <summary>Registers the peek's chrome intent; cleared via <see cref="ClearPeekInsetIntent(AView)"/> (or the parameterless overload).</summary>
    public void SetPeekInsetIntent(AView peekView, int topInsetPx, int bottomInsetPx)
        => _peekIntent = (peekView, topInsetPx, bottomInsetPx);

    /// <summary>Clears the peek intent, tolerating stale calls for a view that is no longer the peek.</summary>
    public void ClearPeekInsetIntent(AView peekView)
    {
        if (_peekIntent is { } intent && ReferenceEquals(intent.View, peekView))
        {
            _peekIntent = null;
        }
    }

    /// <summary>
    /// Unconditional clear for the sync taking over the presentation: from that point the
    /// layer-wide intent describes the incoming page (a committed peek was precomputed with
    /// the same values), and a lingering per-view override would fight later chrome changes.
    /// </summary>
    public void ClearPeekInsetIntent() => _peekIntent = null;

    /// <summary>Overrides the layer-wide rewrite on the peek subtree (runs AFTER it, so it wins).</summary>
    private void RedispatchPeekIntent()
    {
        if (_peekIntent is { } peek && _lastRawInsets is { } raw)
        {
            ViewCompat.DispatchApplyWindowInsets(peek.View, Rewrite(raw, peek.TopInsetPx, peek.BottomInsetPx));
        }
    }

    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
    {
        // A Pan-mode layer is translated: MAUI derives padding from on-screen positions, so the
        // layer sits at rest for the length of the (synchronous) dispatch — same idea as the
        // transition parking below.
        var pan = TranslationY;

        if (pan != 0)
        {
            TranslationY = 0;
        }

        try
        {
            return DispatchApplyWindowInsetsAtRest(insets);
        }
        finally
        {
            if (pan != 0)
            {
                TranslationY = pan;
            }
        }
    }

    private WindowInsets? DispatchApplyWindowInsetsAtRest(WindowInsets? insets)
    {
        if (!TransitionInFlight)
        {
            var result = base.DispatchApplyWindowInsets(insets);
            RedispatchPeekIntent();

            return result;
        }

        // Mid-transition the pages are TRANSFORMED, and MAUI derives a page's safe-area padding
        // from its on-screen position: a dispatch landing now would pad each page for where it
        // momentarily sits, and that padding survives at rest. Rather than swallow the dispatch —
        // which leaves a page mounted mid-transition laid out against stale insets until the
        // transition ends, so its content visibly JUMPS into place — the transforms are parked at
        // rest for the length of the dispatch. It is synchronous, so no frame is drawn in
        // between: the pages keep moving, and they are padded for where they will LAND.
        var parked = ParkTransforms();

        try
        {
            var result = base.DispatchApplyWindowInsets(insets);
            RedispatchPeekIntent();

            return result;
        }
        finally
        {
            foreach (var (view, translationX, translationY, scaleX, scaleY, alpha) in parked)
            {
                view.TranslationX = translationX;
                view.TranslationY = translationY;
                view.ScaleX = scaleX;
                view.ScaleY = scaleY;
                view.Alpha = alpha;
            }
        }
    }

    /// <summary>Resets every hosted page to its resting geometry, returning what to restore.</summary>
    private List<(AView View, float TranslationX, float TranslationY, float ScaleX, float ScaleY, float Alpha)> ParkTransforms()
    {
        var parked = new List<(AView, float, float, float, float, float)>();

        void Park(AView? view)
        {
            if (view is null)
            {
                return;
            }

            parked.Add((view, view.TranslationX, view.TranslationY, view.ScaleX, view.ScaleY, view.Alpha));
            view.TranslationX = 0f;
            view.TranslationY = 0f;
            view.ScaleX = 1f;
            view.ScaleY = 1f;
            view.Alpha = 1f;
        }

        for (var i = 0; i < ChildCount; i++)
        {
            var layerChild = GetChildAt(i);

            // A peek (predictive back) is a page view directly in the layer; pages otherwise sit
            // one level down, inside the fragment container.
            Park(layerChild);

            if (layerChild is ViewGroup hosts)
            {
                for (var j = 0; j < hosts.ChildCount; j++)
                {
                    Park(hosts.GetChildAt(j));
                }
            }
        }

        return parked;
    }

    // Intercepting mid-gesture cancels whatever a page had started (children get ACTION_CANCEL);
    // consuming the event keeps it from falling through to anything behind the layer.
    public override bool OnInterceptTouchEvent(MotionEvent? ev) => TransitionInFlight || base.OnInterceptTouchEvent(ev);

    public override bool OnTouchEvent(MotionEvent? e) => TransitionInFlight || base.OnTouchEvent(e);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        }
    }

    /// <summary>
    /// Replays the CURRENT rewrite to a page mounted between two window dispatches.
    /// A freshly added view gets no dispatch of its own — the window only re-dispatches when the
    /// insets themselves change — so a pushed page would lay out against the raw system bars and
    /// slide its content under the nav bar strip. Rewriting from the last raw insets (rather than
    /// replaying the last rewritten ones) picks up the chrome intent the presenter set for THIS
    /// page, and dispatching to that view alone leaves the outgoing page's layout untouched
    /// mid-transition.
    /// </summary>
    public void ApplyInsetsTo(AView pageView)
    {
        if (_lastRawInsets is { } raw)
        {
            var rewritten = _peekIntent is { } peek && ReferenceEquals(peek.View, pageView)
                ? Rewrite(raw, peek.TopInsetPx, peek.BottomInsetPx)
                : Rewrite(raw);

            ViewCompat.DispatchApplyWindowInsets(pageView, rewritten);
        }

        // ...and ask the window for a real pass too: MAUI computes a hosted view's safe-area
        // padding in its OWN listener (registered for whole subtrees, not per view), which a
        // hand-rolled dispatch to a single child does not run.
        ViewCompat.RequestApplyInsets(pageView);
    }

    WindowInsetsCompat? AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);

        _lastRawInsets = insets;

        return Rewrite(insets);
    }

    private WindowInsetsCompat Rewrite(WindowInsetsCompat insets)
        => Parent is ScaffoldLayout { } scaffoldLayout
            ? Rewrite(insets, scaffoldLayout.PageTopInsetPx, scaffoldLayout.PageBottomInsetPx)
            : insets;

    /// <summary>
    /// The chrome rewrite (§5.4), plus the soft keyboard per the page's policy: under Resize the
    /// keyboard's running overlap is folded into the bottom system inset (it covers the bar and the
    /// system inset alike, so it REPLACES them: max), and the IME inset itself is zeroed so MAUI's
    /// own SoftInput handling does not double-pad; under Pan the IME is zeroed (the layer slides
    /// instead); under None the insets pass through untouched (MAUI's SoftInput semantics apply).
    /// </summary>
    private WindowInsetsCompat Rewrite(WindowInsetsCompat insets, int topInsetPx, int bottomInsetPx)
    {
        var scaffoldLayout = Parent as ScaffoldLayout;
        var mode = scaffoldLayout?.PageKeyboardMode?.Invoke() ?? ScaffoldKeyboardMode.Resize;
        var keyboardPx = scaffoldLayout?.PageKeyboardOverlapPx ?? 0;

        if (mode == ScaffoldKeyboardMode.None)
        {
            return RewriteChrome(insets, topInsetPx, bottomInsetPx);
        }

        var systemBarInsets = insets.GetInsets(_systemBarsInsetsType) ?? throw new InvalidOperationException("SystemBars insets are null.");
        var bottom = bottomInsetPx > 0 ? bottomInsetPx : systemBarInsets.Bottom;

        if (mode == ScaffoldKeyboardMode.Resize)
        {
            bottom = Math.Max(bottom, keyboardPx);
        }

        var modifiedSystemBarInsets = Insets.Of(
            systemBarInsets.Left,
            topInsetPx > 0 ? topInsetPx : systemBarInsets.Top,
            systemBarInsets.Right,
            bottom
        )!;

        using var builder = new WindowInsetsCompat.Builder(insets);

        return builder
               .SetInsets(_systemBarsInsetsType, modifiedSystemBarInsets)!
               .SetInsets(_imeInsetsType, Insets.None!)!
               .SetVisible(_imeInsetsType, false)!
               .Build()
               ?? insets;
    }

    private static WindowInsetsCompat RewriteChrome(WindowInsetsCompat insets, int topInsetPx, int bottomInsetPx)
    {
        if (topInsetPx > 0 || bottomInsetPx > 0)
        {
            var systemBarInsets = insets.GetInsets(_systemBarsInsetsType) ?? throw new InvalidOperationException("SystemBars insets are null.");

            var modifiedSystemBarInsets = Insets.Of(
                systemBarInsets.Left,
                topInsetPx > 0 ? topInsetPx : systemBarInsets.Top,
                systemBarInsets.Right,
                bottomInsetPx > 0 ? bottomInsetPx : systemBarInsets.Bottom
            )!;

            using var builder = new WindowInsetsCompat.Builder(insets);

            return builder
                   .SetInsets(_systemBarsInsetsType, modifiedSystemBarInsets)!
                   .Build()
                   ?? insets;
        }

        return insets;
    }
}

/// <summary>
/// Bottom chrome strip hosting the MAUI tab bar platform view: the bar FILLS the strip flush
/// to the screen's bottom edge and OWNS the bottom system inset (SafeAreaEdges semantics,
/// symmetric with the nav strip) — a consuming bar measures inset-included, an edge-to-edge
/// bar measures content-only. Stays touch-transparent outside the bar content. Includes the
/// MauiWindowInsetListener registration required on .NET 10 so hosted MAUI views participate
/// in the insets chain.
/// </summary>
internal sealed class ScaffoldTabBarStripLayout : FrameLayout
{
    private AView? _bar;
    private int _barMeasuredHeight;
    private bool _insetsFrozen;

    public AView? Bar => _bar;

    /// <summary>
    /// Freezes the insets seen by the hosted bar while the strip is translated for a
    /// hide/show slide: the net10 MauiWindowInsetListener recomputes child safe-area padding
    /// from ON-SCREEN bounds on every insets dispatch, so a strip sliding through the
    /// system-bars region would inflate the bar by the overlap (and the stale padding
    /// survived back at rest). While frozen, dispatches are swallowed and the bar keeps its
    /// resting padding.
    /// </summary>
    internal void FreezeInsets() => _insetsFrozen = true;

    /// <summary>Unfreezes insets; when requested, re-dispatches so children recompute at rest.</summary>
    internal void UnfreezeInsets(bool requestApply = true)
    {
        if (!_insetsFrozen)
        {
            return;
        }

        _insetsFrozen = false;

        if (requestApply)
        {
            ViewCompat.RequestApplyInsets(this);
        }
    }

    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
        => _insetsFrozen ? insets : base.DispatchApplyWindowInsets(insets);

    public ScaffoldTabBarStripLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
    public ScaffoldTabBarStripLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);

        var type = Type.GetType("Microsoft.Maui.Platform.MauiWindowInsetListener, Microsoft.Maui")
                   ?? throw new NotSupportedException("The MAUI version you are using is not supported because MauiWindowInsetListener is missing.");

        type
            .GetMethod("RegisterParentForChildViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [this, null]);
    }

    public void SetBar(AView? bar)
    {
        if (_bar?.Parent?.Handle == Handle)
        {
            RemoveView(_bar);
        }

        _bar = bar;

        if (bar is not null)
        {
            (bar.Parent as ViewGroup)?.RemoveView(bar);
            AddView(bar);

            // The bar's shadow (e.g. the default pill) must not truncate at layout bounds.
            ScaffoldChromeLayoutHelpers.DisableChildClipping(bar);
        }
    }

    private static readonly int _unspecifiedHeightSpec = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (_bar is null)
        {
            SetMeasuredDimension(0, 0);

            return;
        }

        // Height UNSPECIFIED: stretchy custom roots (star rows) would fill any finite spec
        // newer ConstraintLayout/Android versions hand down. The BAR owns the bottom system
        // inset (SafeAreaEdges semantics, nav-strip parity): a consuming bar measures with its
        // self-applied inset padding included, an edge-to-edge bar measures content only — the
        // strip is exactly the bar's measured height, nothing added.
        _bar.Measure(widthMeasureSpec, _unspecifiedHeightSpec);
        _barMeasuredHeight = _bar.MeasuredHeight;

        SetMeasuredDimension(_bar.MeasuredWidth, _barMeasuredHeight);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        // The bar FILLS the strip, system-inset region included: custom bars can paint under
        // the system navigation area (their SafeAreaEdges decides any inner padding), while
        // the default template's Auto-row root keeps its pill above the inset (Auto rows
        // top-align at their measured height).
        var width = right - left;
        var height = bottom - top;

        if (_bar is LayoutViewGroup layoutViewGroup)
        {
            // MAUI layout roots need the explicit cross-platform arrange call (net10 behavior,
            // mirrored from NaluShellItemRendererNavigationLayout).
            layoutViewGroup.Layout(0, 0, width, height);
        }
        else
        {
            _bar?.Layout(0, 0, width, height);
        }
    }

    public override bool OnTouchEvent(MotionEvent? e)
        // Touch-transparent glass: only the bar's own children consume touches — taps on the
        // pill's side margins must reach the page below.
        => false;
}

/// <summary>
/// Top chrome strip hosting the MAUI nav bar platform view. Unlike the tab bar strip, the bar
/// view FILLS the strip (its background extends under the status bar) and consumes the
/// safe-area padding itself (SafeAreaEdges via the MauiWindowInsetListener registration) —
/// the measured height therefore already includes the status inset.
/// </summary>
internal sealed class ScaffoldNavBarStripLayout : FrameLayout
{
    private AView? _bar;
    private int _barMeasuredHeight;
    private bool _insetsFrozen;

    public AView? Bar => _bar;

    /// <summary>Same freeze contract as <see cref="ScaffoldTabBarStripLayout.FreezeInsets"/> for the top strip.</summary>
    internal void FreezeInsets() => _insetsFrozen = true;

    /// <summary>Unfreezes insets; when requested, re-dispatches so children recompute at rest.</summary>
    internal void UnfreezeInsets(bool requestApply = true)
    {
        if (!_insetsFrozen)
        {
            return;
        }

        _insetsFrozen = false;

        if (requestApply)
        {
            ViewCompat.RequestApplyInsets(this);
        }
    }

    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
        => _insetsFrozen ? insets : base.DispatchApplyWindowInsets(insets);

    public ScaffoldNavBarStripLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
    public ScaffoldNavBarStripLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);

        var type = Type.GetType("Microsoft.Maui.Platform.MauiWindowInsetListener, Microsoft.Maui")
                   ?? throw new NotSupportedException("The MAUI version you are using is not supported because MauiWindowInsetListener is missing.");

        type
            .GetMethod("RegisterParentForChildViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [this, null]);
    }

    public void SetBar(AView? bar)
    {
        if (_bar?.Parent?.Handle == Handle)
        {
            RemoveView(_bar);
        }

        _bar = bar;

        if (bar is not null)
        {
            (bar.Parent as ViewGroup)?.RemoveView(bar);
            AddView(bar);

            // The bar's shadow (e.g. the default pill) must not truncate at layout bounds.
            ScaffoldChromeLayoutHelpers.DisableChildClipping(bar);
        }
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (_bar is null)
        {
            SetMeasuredDimension(0, 0);

            return;
        }

        MeasureChild(_bar, widthMeasureSpec, heightMeasureSpec);
        _barMeasuredHeight = _bar.MeasuredHeight;
        SetMeasuredDimension(_bar.MeasuredWidth, _barMeasuredHeight);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        if (_bar is LayoutViewGroup layoutViewGroup)
        {
            layoutViewGroup.Layout(0, 0, right - left, _barMeasuredHeight);
        }
        else
        {
            _bar?.Layout(0, 0, right - left, _barMeasuredHeight);
        }
    }

    public override bool OnTouchEvent(MotionEvent? e)
        // Touch-transparent glass outside the bar content.
        => false;
}
