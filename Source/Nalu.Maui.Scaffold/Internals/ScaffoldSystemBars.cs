namespace Nalu.Internals;

/// <summary>Everything the system-bar style resolution looks at, as one immutable snapshot (unit-testable).</summary>
internal readonly record struct ScaffoldSystemBarSnapshot(
    ScaffoldSystemBarStyle PageStyle,
    ScaffoldSystemBarStyle AreaStyle,
    ScaffoldSystemBarStyle ScaffoldStyle,
    bool NavBarVisible,
    Brush? BarBackground,
    double BarOpacity,
    Color? OverlaySurface,
    Color? PageSurface,
    double? SampledLuminance,
    bool DarkTheme);

/// <summary>
/// Per-scaffold owner of the system status/navigation bar ICON style: contributors (the nav bar
/// host, the presenters) push state changes, the effective style is re-resolved, and the
/// platform applier is invoked only when the result flips. The resolution follows the visible
/// surface stack (§ system bars):
/// open flyout → nav bar (visible AND opaque enough, by luminance — live, so scroll-driven
/// backgrounds flip the icons exactly when the bar materializes) → the page's declared
/// <see cref="Scaffold.SystemBarStyleProperty"/> (page → area → scaffold) → the SAMPLED
/// luminance of the actual rendered status-bar strip (the platform pixel sampler — ground
/// truth for photos and other unknowable content) → the page's own top-of-screen surface
/// color → the app theme.
/// Samples are event-driven and debounced: contributors schedule one on visual changes, the
/// presenters request one at presentation settle points (a sample must read RENDERED pixels —
/// never a mid-transition frame), and the last good sample is kept until replaced so the value
/// never flickers between the sampled and semantic layers.
/// </summary>
internal sealed class ScaffoldSystemBars(Scaffold scaffold)
{
    private const double _opacityThreshold = 0.5;
    private const int _sampleDebounceMs = 80;

    private Action<bool>? _applier;
    private Action? _themeRefresher;
    private Func<Task<double?>>? _sampler;
    private bool? _lightIcons;
    private Page? _page;
    private Brush? _barBackground;
    private double _barOpacity = 1;
    private bool _navBarVisible;
    private Color? _overlaySurface;
    private double? _sampledLuminance;
    private bool _sampleScheduled;

    /// <summary>
    /// Installs (or removes, with null) the platform applier. Theme changes are observed only
    /// while an applier is attached; installing re-applies the current resolution unconditionally.
    /// </summary>
    public void SetApplier(Action<bool>? applier)
    {
        if (_applier is null && applier is not null && Application.Current is { } application)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
        }
        else if (_applier is not null && applier is null && Application.Current is { } detachApplication)
        {
            detachApplication.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        _applier = applier;
        _lightIcons = null;
        Recompute();
    }

    /// <summary>
    /// Installs (or removes) the platform pixel sampler: an async read of the average luminance
    /// [0, 1] of the app content rendered under the status bar (null = sample unavailable).
    /// </summary>
    public void SetSampler(Func<Task<double?>>? sampler)
    {
        _sampler = sampler;
        ScheduleSample();
    }

    /// <summary>
    /// Installs (or removes) a platform refresh invoked on every app-theme change, for window
    /// state resolved from THEME ATTRIBUTES only at activity creation (Android's
    /// navigationBarColor) — without a recreation those go stale on a system theme toggle.
    /// </summary>
    public void SetThemeRefresher(Action? themeRefresher) => _themeRefresher = themeRefresher;

    /// <summary>The nav bar host pushes the current page + the LIVE effective bar background here (per-frame safe).</summary>
    public void UpdateBar(Page? page, Brush? barBackground, double barOpacity)
    {
        if (!ReferenceEquals(_page, page))
        {
            // New page: its pixels are not on screen yet — the stale sample must not linger.
            _sampledLuminance = null;
        }

        _page = page;
        _barBackground = barBackground;
        _barOpacity = barOpacity;
        ScheduleSample();
        Recompute();
    }

    /// <summary>Whether the nav bar strip is currently shown for the presented page (set by the presenters).</summary>
    public bool NavBarVisible
    {
        get => _navBarVisible;
        set
        {
            _navBarVisible = value;
            ScheduleSample();
            Recompute();
        }
    }

    /// <summary>
    /// The surface color of an overlay covering the status-bar region (the open flyout), or null
    /// when none — the topmost layer of the resolution when present.
    /// </summary>
    public Color? OverlaySurface
    {
        get => _overlaySurface;
        set
        {
            _overlaySurface = value;
            ScheduleSample();
            Recompute();
        }
    }

    /// <summary>
    /// The presenters call this when a presentation fully settled (page transition done, chrome
    /// at rest): the pixels are final — discard whatever was sampled before and read fresh.
    /// </summary>
    public void OnPresentationSettled()
    {
        _sampledLuminance = null;
        ScheduleSample();
        Recompute();
    }

    /// <summary>Re-resolves and applies when the effective style flipped (also called for declaration/theme changes).</summary>
    public void Recompute()
    {
        if (_applier is null)
        {
            return;
        }

        var light = ResolveLightIcons(Snapshot());

        if (_lightIcons != light)
        {
            _lightIcons = light;
            _applier(light);
        }
    }

    private ScaffoldSystemBarSnapshot Snapshot()
        => new(
            _page is not null ? Scaffold.GetSystemBarStyle(_page) : ScaffoldSystemBarStyle.Auto,
            scaffold.CurrentArea is { } area ? Scaffold.GetSystemBarStyle(area) : ScaffoldSystemBarStyle.Auto,
            Scaffold.GetSystemBarStyle(scaffold),
            _navBarVisible,
            _barBackground,
            _barOpacity,
            _overlaySurface,
            PageSurfaceColor(_page),
            _sampledLuminance,
            IsDarkTheme());

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        // Theme resources repaint everything: the previous pixels are meaningless.
        _sampledLuminance = null;
        _themeRefresher?.Invoke();
        ScheduleSample();
        Recompute();
    }

    /// <summary>
    /// Debounced sampling (UI thread): visual-change bursts (per-frame scroll appearance)
    /// coalesce into one read shortly after; the result replaces the stored sample and
    /// re-resolves. A failed read keeps the previous sample (semantic layers cover the gap).
    /// </summary>
    private void ScheduleSample()
    {
        if (_sampler is null || _applier is null || _sampleScheduled)
        {
            return;
        }

        _sampleScheduled = true;

        _ = RunSampleAsync();

        async Task RunSampleAsync()
        {
            try
            {
                await Task.Delay(_sampleDebounceMs).ConfigureAwait(true);
                _sampleScheduled = false;

                if (_sampler is { } sampler && await sampler().ConfigureAwait(true) is { } luminance)
                {
                    _sampledLuminance = luminance;
                    Recompute();
                }
            }
            catch
            {
                _sampleScheduled = false;
            }
        }
    }

    private static bool IsDarkTheme()
        => Application.Current is { } application
            && (application.UserAppTheme is AppTheme.Unspecified ? application.RequestedTheme : application.UserAppTheme) == AppTheme.Dark;

    /// <summary>Pure resolution: true = light (white) icons, false = dark icons.</summary>
    public static bool ResolveLightIcons(in ScaffoldSystemBarSnapshot snapshot)
    {
        // 1. An overlay covering the status-bar region is the topmost surface.
        if (snapshot.OverlaySurface is { } overlay)
        {
            return IsDarkSurface(overlay);
        }

        // 2. A visible, opaque-enough nav bar IS the surface — it outranks even an explicit
        //    declaration (which describes the page's own content, not the chrome above it).
        if (snapshot.NavBarVisible
            && EffectiveColor(snapshot.BarBackground, snapshot.BarOpacity) is { } barColor)
        {
            return IsDarkSurface(barColor);
        }

        // 3. Explicit declaration, page → area → scaffold.
        foreach (var style in (ReadOnlySpan<ScaffoldSystemBarStyle>)[snapshot.PageStyle, snapshot.AreaStyle, snapshot.ScaffoldStyle])
        {
            if (style is not ScaffoldSystemBarStyle.Auto)
            {
                return style is ScaffoldSystemBarStyle.LightContent;
            }
        }

        // 4. The sampled ground truth of the rendered strip — covers photos and every other
        //    surface the semantic layers below cannot know.
        if (snapshot.SampledLuminance is { } sampled)
        {
            return sampled < 0.5;
        }

        // 5. The page's own top-of-screen surface, when its color is knowable.
        if (snapshot.PageSurface is { } pageSurface)
        {
            return IsDarkSurface(pageSurface);
        }

        // 6. Theme default: the theme background is the surface.
        return snapshot.DarkTheme;
    }

    /// <summary>
    /// The page's top-of-screen surface color: the first child's background when it spans the
    /// top edge (SafeAreaEdges None on top), else the page's own background — null when neither
    /// resolves to a usable color (image content, gradients under threshold, defaults).
    /// </summary>
    private static Color? PageSurfaceColor(Page? page)
    {
        if (page is ContentPage { Content: { } content } contentPage
            && (TopEdgeNone(SafeAreaEdgesOf(content)) || TopEdgeNone(contentPage.SafeAreaEdges))
            && (EffectiveColor(content.Background, 1) ?? OpaqueOrNull(content.BackgroundColor)) is { } contentColor)
        {
            return contentColor;
        }

        return page is null ? null : EffectiveColor(page.Background, 1) ?? OpaqueOrNull(page.BackgroundColor);
    }

    private static bool TopEdgeNone(SafeAreaEdges? edges) => edges?.Top == SafeAreaRegions.None;

    /// <summary>SafeAreaEdges is declared per control type — no common base surfaces it.</summary>
    private static SafeAreaEdges? SafeAreaEdgesOf(View view)
        => view switch
        {
            Layout layout => layout.SafeAreaEdges,
            ScrollView scroll => scroll.SafeAreaEdges,
            ContentView contentView => contentView.SafeAreaEdges,
            Border border => border.SafeAreaEdges,
            _ => null
        };

    /// <summary>
    /// The solid color a brush contributes to the surface, or null when it is (effectively)
    /// transparent or unknowable. Gradients contribute their first stop.
    /// </summary>
    private static Color? EffectiveColor(Brush? brush, double opacity)
    {
        var color = brush switch
        {
            SolidColorBrush solid => solid.Color,
            GradientBrush { GradientStops: { Count: > 0 } stops } => stops[0].Color,
            _ => null
        };

        if (color is null)
        {
            return null;
        }

        return color.Alpha * opacity >= _opacityThreshold ? color : null;
    }

    /// <summary>The surface color an overlay view (flyout) presents: its own background, else a theme surface.</summary>
    public static Color SurfaceColorOf(View view)
        => EffectiveColor(view.Background, 1)
            ?? OpaqueOrNull(view.BackgroundColor)
            ?? (IsDarkTheme() ? Color.FromRgb(0.07f, 0.07f, 0.07f) : Colors.White);

    private static Color? OpaqueOrNull(Color? color)
        => color is { Alpha: >= (float)_opacityThreshold } ? color : null;

    /// <summary>Relative luminance under 0.5 reads as a dark surface (→ light icons).</summary>
    private static bool IsDarkSurface(Color color)
        => (0.2126 * color.Red) + (0.7152 * color.Green) + (0.0722 * color.Blue) < 0.5;
}
