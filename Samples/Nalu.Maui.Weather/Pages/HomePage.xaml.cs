using Nalu.Maui.Weather.PageModels;

namespace Nalu.Maui.Weather.Pages;

public partial class HomePage
{
    public HomePage(HomePageModel model)
    {
        BindingContext = model;
        InitializeComponent();

        ScrollableArea.Scrolled += ScrollableArea_Scrolled;
    }

    private void ScrollableArea_Scrolled(object? sender, ScrolledEventArgs e)
    {
        // Parallax effect for the banner image :)
        BannerImage.TranslationY = e.ScrollY / 3;
    }
}
