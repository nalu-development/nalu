using System.ComponentModel;

namespace Nalu.Internals;

/// <summary>
/// The library-owned virtual realization of the nav bar strip: hosts the resolved bar view
/// (default or custom) and applies the effective <see cref="ScaffoldNavBarAppearance"/> to
/// itself — never to the mounted bar, whose own properties (and styles) stay untouched.
/// One instance per presenter lifetime; the platform strip mounts its platform view once and
/// bar swaps happen virtually through <see cref="SetBar"/>.
/// </summary>
/// <remarks>
/// Appearance values land on the INNER container: the host itself is platform-framed by the
/// strip, so its virtual frame is never arranged and the iOS transform mapper would silently
/// skip translations on it (the bottom-sheet lesson) — the inner container IS virtually
/// arranged by the host's cross-platform layout pass, making <c>TranslationY</c> reliable.
/// </remarks>
internal sealed class ScaffoldNavBarHost : Grid, IDisposable
{
    private readonly Scaffold _scaffold;
    private readonly Grid _content;
    private readonly SolidColorBrush _defaultBackground = new(ScaffoldNavBarAppearance._defaultBackgroundColor);

    private View? _bar;
    private Page? _page;
    private ScaffoldArea? _area;
    private IDisposable? _scrollObservation;
    private ScaffoldNavBarAppearance? _pageAppearance;
    private ScaffoldNavBarAppearance? _areaAppearance;
    private ScaffoldNavBarAppearance? _scaffoldAppearance;

    public View? Bar => _bar;

    /// <summary>
    /// Raised when a measure invalidation from ANYWHERE in the hosted bar's subtree bubbles
    /// through the host. The platform strip cannot rely on MAUI's platform-level invalidation
    /// walk to deliver this: with the host chain between the bar and the strip, the walk dies at
    /// a <c>MauiView</c> whose propagation latch was never reset (its measure was answered from a
    /// virtual cache, so its platform <c>SizeThatFits</c> — the latch reset — never ran). The
    /// CONTROLS-layer bubble used here climbs the LOGICAL tree unconditionally, so a runtime bar
    /// height change (badge growing the bar, large-title expansion) reliably reaches the strip.
    /// </summary>
    public Action? BarMeasureInvalidated { get; set; }

    public ScaffoldNavBarHost(Scaffold scaffold)
    {
        _scaffold = scaffold;
        MeasureInvalidated += (_, _) => BarMeasureInvalidated?.Invoke();

        // Chrome never self-pads: the mounted bar view owns its safe-area behavior (the default
        // bar consumes the top inset itself), and on Android an explicit None also dodges the
        // net10 off-screen first-traversal padding heuristic.
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);

        _content = new Grid
        {
            AutomationId = "NavBarSurface",
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
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
            bar.BindingContext = _scaffold.NavBarContext;
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
        // "NavBarAppearance" covers attachment replacement at any level; "CurrentArea" retargets
        // the area subscription when navigation switches areas.
        if (e.PropertyName is "NavBarAppearance" or nameof(Scaffold.CurrentArea))
        {
            RefreshAppearanceChain();
        }
        else if (e.PropertyName == "ScrollTracker")
        {
            UpdateScrollTracking();
        }
        else if (e.PropertyName == "SystemBarStyle")
        {
            _scaffold.SystemBars.Recompute();
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

        var context = _scaffold.NavBarContext;

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

        var (page, areaAppearance, scaffoldAppearance) = _scaffold.GetNavBarAppearanceChain(_page);
        SwapAppearanceSubscription(ref _pageAppearance, page);
        SwapAppearanceSubscription(ref _areaAppearance, areaAppearance);
        SwapAppearanceSubscription(ref _scaffoldAppearance, scaffoldAppearance);

        ApplyEffectiveAppearance();
    }

    private void SwapAppearanceSubscription(ref ScaffoldNavBarAppearance? field, ScaffoldNavBarAppearance? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        if (field is not null)
        {
            field.PropertyChanged -= OnAppearancePropertyChanged;

            // Only clear the ambient-context stamp when the object left the chain entirely
            // (one instance can sit at two levels of the chain at once).
            if (!IsInChain(field))
            {
                field.SetContext(null);
            }
        }

        field = value;

        if (value is not null)
        {
            value.PropertyChanged += OnAppearancePropertyChanged;
            value.SetContext(_scaffold.NavBarContext);
        }
    }

    private bool IsInChain(ScaffoldNavBarAppearance appearance)
        => ReferenceEquals(_pageAppearance, appearance)
            || ReferenceEquals(_areaAppearance, appearance)
            || ReferenceEquals(_scaffoldAppearance, appearance);

    private void OnAppearancePropertyChanged(object? sender, PropertyChangedEventArgs e)
        => ApplyEffectiveAppearance();

    /// <summary>
    /// Recomputes and applies every effective value — cheap enough (four resolutions over at
    /// most three objects) to run whole on any change, which is what makes per-frame
    /// animation of a page appearance viable.
    /// </summary>
    private void ApplyEffectiveAppearance()
    {
        _content.Background = ScaffoldNavBarAppearance.Resolve<Brush?>(
            ScaffoldNavBarAppearance.BackgroundProperty, _pageAppearance, _areaAppearance, _scaffoldAppearance, _defaultBackground);

        _content.Opacity = ScaffoldNavBarAppearance.Resolve(
            ScaffoldNavBarAppearance.OpacityProperty, _pageAppearance, _areaAppearance, _scaffoldAppearance, 1.0);

        _content.TranslationY = ScaffoldNavBarAppearance.Resolve(
            ScaffoldNavBarAppearance.OffsetYProperty, _pageAppearance, _areaAppearance, _scaffoldAppearance, 0.0);

        _scaffold.NavBarContext.Foreground = ScaffoldNavBarAppearance.Resolve<Color?>(
            ScaffoldNavBarAppearance.ForegroundProperty, _pageAppearance, _areaAppearance, _scaffoldAppearance, null);

        _scaffold.NavBarContext.TitleForeground = ScaffoldNavBarAppearance.ResolveTitleForeground(_pageAppearance, _areaAppearance, _scaffoldAppearance);

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
        SwapAppearanceSubscription(ref _pageAppearance, null);
        SwapAppearanceSubscription(ref _areaAppearance, null);
        SwapAppearanceSubscription(ref _scaffoldAppearance, null);

        if (_area is not null)
        {
            _area.PropertyChanged -= OnElementPropertyChanged;
            _area = null;
        }

        SetBar(null);
    }
}
