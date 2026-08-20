using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;
using Nalu.Internals;
using AView = Android.Views.View;
using Insets = AndroidX.Core.Graphics.Insets;

namespace Nalu;

/// <summary>
/// The platform realization of one <see cref="ScaffoldPageHost"/>: the page's view and the
/// page's OWN nav bar strip, SIBLINGS inside a container the scaffold owns. The frame is the
/// page fragment's view, so every motion the presenter plays — push/pop slides, custom specs,
/// shared elements, the predictive-back peek — moves the bar with its page for free.
/// </summary>
/// <remarks>
/// <para>
/// Inset ownership is the reason the two are siblings rather than nested. The page's top inset
/// is DERIVED from the strip, so the strip must never receive it: the frame dispatches the
/// insets it was handed to the strip unchanged (the bar consumes the real status inset itself
/// through its own SafeAreaEdges) and dispatches a top-rewritten copy to the page view alone.
/// Nested — the strip inside the page's inset path — that is a feedback loop: strip height
/// becomes the page's top inset, the bar consumes it and grows, which grows the strip, which
/// grows the inset. It compounds every pass and the bar eats the screen.
/// </para>
/// <para>
/// This is the same split iOS gets from <c>AdditionalSafeAreaInsets</c> on the page's view
/// controller: one writer for the page's extra top inset, and chrome that is never a descendant
/// of what it insets.
/// </para>
/// <para>
/// It is a MANAGED view subclass sitting in the page-host chain, which the presenter otherwise
/// avoids because a managed Java peer defers the GC-bridge release of popped pages past the
/// leak detector's patience. <see cref="Release"/> therefore drops every managed reference and
/// unregisters the listener rather than trusting the bridge — the Android twin of the iOS
/// container leak. <c>ScaffoldNavigationTests</c> asserts <c>Leaked:0</c> after every test.
/// </para>
/// </remarks>
// ReSharper disable once RedundantNameQualifier — inside a View subclass the bare name binds to the nested Android.Views.View.IOnApplyWindowInsetsListener
internal sealed class ScaffoldPageFrame : FrameLayout, AndroidX.Core.View.IOnApplyWindowInsetsListener
{
    private static readonly int _systemBarsInsetsType = WindowInsetsCompat.Type.SystemBars();

    private ScaffoldPageHost? _host;
    private ScaffoldNavBarStripLayout? _strip;
    private AView? _pageView;
    private WindowInsetsCompat? _lastInsets;
    private int _appliedTopInsetPx = -1;

    public ScaffoldPageFrame(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public ScaffoldPageFrame(Context context, ScaffoldPageHost host)
        : base(context)
    {
        _host = host;
        LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        SetClipChildren(false);
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
    }

    /// <summary>This page's nav bar strip, when it shows one.</summary>
    public ScaffoldNavBarStripLayout? NavStrip => _strip;

    /// <summary>Mounts the page's platform view (below the bar) — called once, at creation.</summary>
    public void SetPageView(AView pageView)
    {
        _pageView = pageView;

        pageView.LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent);

        AddView(pageView);
    }

    /// <summary>
    /// Mounts (or drops) this page's bar from the resolved template. The strip is added ABOVE the
    /// page view: the bar always draws over page content, and an overlapping page simply gets no
    /// top inset.
    /// </summary>
    public void SyncNavBar(IMauiContext mauiContext)
    {
        if (_host is not { } host)
        {
            return;
        }

        var visible = host.IsNavBarVisible;

        // A page that shows no bar never gets a strip: nothing to measure wrongly, nothing to
        // flash during a transition.
        if (!visible && _strip is null)
        {
            host.SetNavBarAttached(false);

            return;
        }

        if (host.EnsureNavBarHost() is not { } barHost)
        {
            ReleaseStrip();
            host.SetNavBarAttached(false);

            return;
        }

        if (_strip is null)
        {
            var strip = new ScaffoldNavBarStripLayout(Context!)
            {
                LayoutParameters = new LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent)
                {
                    Gravity = GravityFlags.Top
                }
            };

            strip.SetBar(barHost.ToPlatform(mauiContext));
            _strip = strip;
            AddView(strip);
        }

        // Hidden is GONE, never a translation by a height nobody has measured yet.
        _strip.Visibility = visible ? ViewStates.Visible : ViewStates.Gone;
        host.SetNavBarAttached(visible);

        // The tree shows the INCOMING page's bar from the moment it mounts, and every other
        // page's leaves it. Their strips keep rendering — they live in their own frames — but
        // automation ids stay unique and tooling reads the page being navigated TO rather than
        // whichever bar happens to be enumerated first.
        host.Scaffold.SettleNavBarAttachments();

        RequestLayout();
    }

    /// <summary>
    /// Splits the insets: the strip gets them UNCHANGED, the page view gets the top rewritten to
    /// this page's chrome footprint. Consumed, because the children are dispatched by hand — the
    /// default subtree dispatch would hand both the same values and close the loop.
    /// </summary>
    WindowInsetsCompat? AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        if (insets is null)
        {
            return insets;
        }

        _lastInsets = insets;
        DispatchInsets();

        return WindowInsetsCompat.Consumed;
    }

    private void DispatchInsets()
    {
        if (_lastInsets is not { } insets)
        {
            return;
        }

        if (_strip is not null)
        {
            // Unchanged: the bar consumes the system inset itself, and its height must never
            // depend on the inset derived FROM its height.
            ViewCompat.DispatchApplyWindowInsets(_strip, insets);
        }

        if (_pageView is not null)
        {
            ViewCompat.DispatchApplyWindowInsets(_pageView, RewriteForPage(insets));
        }
    }

    /// <summary>
    /// The page's top inset: its own bar's footprint, which already spans the system inset it
    /// extends under. Zero contribution when the page overlaps its bar or shows none — the raw
    /// system top then reaches the page untouched.
    /// </summary>
    private WindowInsetsCompat RewriteForPage(WindowInsetsCompat insets)
    {
        var footprint = TopInsetPx;

        if (footprint <= 0)
        {
            return insets;
        }

        var systemBars = insets.GetInsets(_systemBarsInsetsType) ?? throw new InvalidOperationException("SystemBars insets are null.");

        using var builder = new WindowInsetsCompat.Builder(insets);

        return builder
               .SetInsets(_systemBarsInsetsType, Insets.Of(systemBars.Left, footprint, systemBars.Right, systemBars.Bottom)!)!
               .Build()
               ?? insets;
    }

    /// <summary>This page's chrome footprint in px, once its strip has been laid out.</summary>
    private int TopInsetPx
        => _host?.WantsNavBarInset == true && _strip is { Visibility: ViewStates.Visible, Height: > 0 } strip
            ? strip.Height
            : 0;

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);

        // The strip's height is only known after it has been laid out, so the page's inset is
        // published from here — and only when it actually changed, or every pass would
        // re-dispatch. The strip's own insets do not depend on it, so this settles in one turn.
        var footprint = TopInsetPx;

        if (footprint != _appliedTopInsetPx)
        {
            _appliedTopInsetPx = footprint;

            if (_pageView is not null && _lastInsets is not null)
            {
                ViewCompat.DispatchApplyWindowInsets(_pageView, RewriteForPage(_lastInsets));

                // ...and ask for a real pass too: MAUI computes a hosted view's safe-area padding
                // in its OWN listener, which a hand-rolled dispatch to one child does not run.
                ViewCompat.RequestApplyInsets(_pageView);
            }
        }
    }

    private void ReleaseStrip()
    {
        if (_strip is not { } strip)
        {
            return;
        }

        strip.SetBar(null);
        RemoveView(strip);
        _strip = null;
    }

    /// <summary>
    /// Drops everything this frame holds. Explicit, not left to the GC bridge: a managed Java
    /// peer in the page-host chain outlives its Dispose, and its managed fields would pin the
    /// page host — and through it the page and its model.
    /// </summary>
    public void Release()
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        ReleaseStrip();
        _pageView = null;
        _lastInsets = null;
        _host = null;
    }
}
