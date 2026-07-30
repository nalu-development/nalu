using Nalu.Maui.DailyHelper.PageModels;

namespace Nalu.Maui.DailyHelper.Pages;

public partial class WeatherDetailPage : ContentPage
{
    // Mirrors the XAML BarProgress converter: one ramp for the whole chrome choreography.
    private const double _fadeStart = 100;
    private const double _fadeLength = 100;

    public WeatherDetailPage(WeatherDetailPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }

    /// <summary>
    /// Scroll-driven chrome, the appearance half: the strip brush gains alpha and the
    /// foreground crossfades white → ink as the hero scrolls away, while the hero itself
    /// pans at half speed (and sticks to the top through the iOS bounce). The TitleView
    /// fade rides the ScrollTracker channel in XAML instead — both recipes on one page.
    /// </summary>
    private void OnDetailScrolled(object? sender, ScrolledEventArgs e)
    {
        var offset = e.ScrollY;

        // Parallax: half-speed pan while leaving; on top overscroll (iOS bounce) follow the
        // offset exactly so the photo stays glued to the screen's top edge. The whole backdrop
        // (photo + darken + scrim) moves as one — the overlays must always cover the image.
        HeroBackdrop.TranslationY = offset < 0 ? offset : offset * 0.5;

        var progress = Math.Clamp((offset - _fadeStart) / _fadeLength, 0d, 1d);
        var dark = (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;

        var barColor = ThemeColor(dark ? "BackgroundDark" : "BackgroundLight");
        BarBrush.Color = barColor.WithAlpha((float)progress);

        var ink = ThemeColor(dark ? "TextPrimaryDark" : "TextPrimaryLight");
        BarAppearance.Foreground = Lerp(Colors.White, ink, (float)progress);
    }

    private static Color ThemeColor(string key)
        => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;

    private static Color Lerp(Color from, Color to, float amount)
        => new(
            from.Red + ((to.Red - from.Red) * amount),
            from.Green + ((to.Green - from.Green) * amount),
            from.Blue + ((to.Blue - from.Blue) * amount),
            from.Alpha + ((to.Alpha - from.Alpha) * amount)
        );
}
