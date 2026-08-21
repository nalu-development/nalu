using System.ComponentModel;

namespace Nalu.Internals;

/// <summary>
/// The library-owned realization of ONE page's nav bar strip: hosts the bar view realized from
/// that page's <see cref="Scaffold.NavBarTemplateProperty"/> and paints the resolved appearance
/// on its own surface — never on the mounted bar, whose properties and styles stay untouched.
/// One instance per page; a bar view change swaps virtually through <see cref="SetBar"/>,
/// keeping the platform strip in place.
/// </summary>
/// <remarks>
/// <para>
/// Layout constraint discipline: the scaffold hands the host <c>HorizontallyFixed</c> (the strip
/// fixes its width, its height follows the bar); star cells of a grid that is not vertically fixed
/// keep the surface and the bar non-<c>Fixed</c> too, so MAUI's PLATFORM measure-invalidation walk
/// (which stops at the first fully Fixed layer) climbs from anywhere in the bar subtree to the
/// native strip, which marks itself dirty and lets the controller re-measure in its layout pass.
/// The Controls-level <c>MeasureInvalidated</c> event is never used.
/// </para>
/// <para>
/// Appearance values land on the INNER container: the host itself is platform-framed by the
/// strip, so its virtual frame is never arranged and the iOS transform mapper would silently
/// skip translations on it (the bottom-sheet lesson) — the inner container IS virtually
/// arranged by the host's cross-platform layout pass, making <c>TranslationY</c> reliable.
/// </para>
/// </remarks>
internal sealed class ScaffoldNavBarHost : Grid, IDisposable
{

    private readonly Scaffold _scaffold;
    private readonly Grid _content;
    private View? _bar;
    private Page? _page;
    private ScaffoldArea? _area;
    private IDisposable? _scrollObservation;

    public View? Bar => _bar;

    /// <summary>
    /// The context this bar shows — its page's. <c>NavBarContextRelay</c> resolves it for every
    /// binding inside the bar subtree (a hosted <see cref="Scaffold.TitleViewProperty"/> view
    /// included), and re-resolves when it is replaced, so bar content never reads another page's
    /// state.
    /// </summary>
    public ScaffoldNavBarContext Context
    {
        get => field ?? _scaffold.NavBarContext;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged();

            _bar?.BindingContext = value;

            UpdateScrollTracking();
            RefreshAppearanceChain();
        }
    }

    public ScaffoldNavBarHost(Scaffold scaffold)
    {
        _scaffold = scaffold;

        // Chrome never self-pads: the mounted bar view owns its safe-area behavior (the default
        // bar consumes the top inset itself), and on Android an explicit None also dodges the
        // net10 off-screen first-traversal padding heuristic.
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);

        // The host FILLS its strip and draws nothing: the bar it carries is what you see, and
        // NavBarOffsetY moves that bar INSIDE this host. So a bar offset out of the way leaves
        // this host still covering the band — and on UIKit, where the deepest view whose bounds
        // contain a point wins whether it drew anything or not, that band stayed dead to touch.
        // Cascade OFF: the bar and everything in it keeps taking its own touches.
        InputTransparent = true;
        CascadeInputTransparent = false;

        _content = new Grid
        {
            AutomationId = "NavBarSurface",
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),

            // The SURFACE is what you can see of the bar, so the surface is what takes the touch.
            // A recognizer that does nothing is the whole mechanism, and it is load-bearing on
            // ANDROID: a MAUI layout consumes nothing there by default, so without this the strip
            // passed on every touch its children did not take and a tap on a visible bar operated
            // the page hidden behind it (removing it fails
            // ScaffoldChromeHitChromeTests.AnOffsetNavBarPassesTouchesThrough on its first
            // assertion). UIKit already gives a view every touch inside its bounds, so on iOS this
            // changes nothing — it is here so both platforms say the same thing in one place.
            // Being a recognizer on the SURFACE is what makes it travel: an offset bar stops
            // claiming the band by construction, with no platform code deciding where the bar
            // "really" is.
            GestureRecognizers = { new TapGestureRecognizer() }
        };

        Add(_content);

        scaffold.PropertyChanged += OnElementPropertyChanged;
        RefreshAppearanceChain();
    }


    /// <summary>Swaps the hosted bar view (no platform re-mount; the strip stays as is).</summary>
    public void SetBar(View? bar)
    {
        if (ReferenceEquals(_bar, bar))
        {
            return;
        }

        if (_bar is { } previous)
        {
            _content.Remove(previous);

            // Release the context: an orphaned bar must stop observing it (its title slot
            // would otherwise keep claiming TitleView content against the mounted bar's).
            // A reusable bar gets the context back on its next mount.
            previous.BindingContext = null;
        }

        _bar = bar;

        if (bar is not null)
        {
            // The bar binds the context — the documented contract for default and custom bars.
            bar.BindingContext = Context;
            _content.Add(bar);
        }
    }

    /// <summary>
    /// Retargets the appearance chain at the current page (invoked by the presenter on every
    /// synchronization); attachment changes on the page/area/scaffold are then observed live.
    /// </summary>
    public void UpdateSources(Page? page)
    {
        if (!ReferenceEquals(_page, page))
        {
            if (_page is not null)
            {
                _page.PropertyChanged -= OnElementPropertyChanged;
            }

            _page = page;

            if (page is not null)
            {
                page.PropertyChanged += OnElementPropertyChanged;
            }

            UpdateScrollTracking();
        }

        RefreshAppearanceChain();
    }

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // The five appearance values are attached properties on the page, the area and the
            // scaffold, so a change arrives as a PropertyChanged on the element it was set on —
            // including per frame while a scroll-driven binding animates one. "CurrentArea"
            // retargets the area subscription when navigation switches areas.
            case "NavBarBackground" or "NavBarOpacity" or "NavBarOffsetY"
                or "NavBarForeground" or "NavBarTitleForeground" or nameof(Scaffold.CurrentArea):
                RefreshAppearanceChain();

                break;
            case "ScrollTracker":
                UpdateScrollTracking();

                break;
        }
    }

    /// <summary>
    /// (Re)binds the scroll channel to the current page's tracked scrollable; without one, the
    /// context reads a resting offset — every page starts its own scroll story.
    /// </summary>
    private void UpdateScrollTracking()
    {
        _scrollObservation?.Dispose();
        _scrollObservation = null;

        var context = Context;

        if (_page is not null && Scaffold.GetScrollTracker(_page) is { } trackedView)
        {
            _scrollObservation = ScaffoldScrollObserver.Observe(trackedView, offset => context.ScrollOffset = offset);
        }
        else
        {
            context.ScrollOffset = 0;
        }
    }

    private void RefreshAppearanceChain()
    {
        var area = _scaffold.CurrentArea;

        if (!ReferenceEquals(_area, area))
        {
            if (_area is not null)
            {
                _area.PropertyChanged -= OnElementPropertyChanged;
            }

            _area = area;

            if (area is not null)
            {
                area.PropertyChanged += OnElementPropertyChanged;
            }
        }

        ApplyEffectiveAppearance();
    }

    /// <summary>
    /// Recomputes and applies every effective value — cheap enough (four resolutions over at
    /// most three objects) to run whole on any change, which is what makes per-frame
    /// animation of a page appearance viable.
    /// </summary>
    private void ApplyEffectiveAppearance()
    {
        _content.Background = (Brush?)_scaffold.ResolveNavBarValue(_page, Scaffold.NavBarBackgroundProperty);
        _content.Opacity = (double)_scaffold.ResolveNavBarValue(_page, Scaffold.NavBarOpacityProperty)!;
        _content.TranslationY = (double)_scaffold.ResolveNavBarValue(_page, Scaffold.NavBarOffsetYProperty)!;
        Context.Foreground = (Color?)_scaffold.ResolveNavBarValue(_page, Scaffold.NavBarForegroundProperty);
        Context.TitleForeground = _scaffold.ResolveNavBarTitleForeground(_page);

        // The system-bar icon style tracks the LIVE bar surface (this runs per-frame during
        // scroll-driven appearance animation — the icons flip exactly when the bar materializes).
        _scaffold.SystemBars.UpdateBar(_page, _content.Background, _content.Opacity);
    }

    public void Dispose()
    {
        _scaffold.PropertyChanged -= OnElementPropertyChanged;
        _scrollObservation?.Dispose();
        _scrollObservation = null;
        UpdateSources(null);

        if (_area is not null)
        {
            _area.PropertyChanged -= OnElementPropertyChanged;
            _area = null;
        }

        SetBar(null);
    }
}
