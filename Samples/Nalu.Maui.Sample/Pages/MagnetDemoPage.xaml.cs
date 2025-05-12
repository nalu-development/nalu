using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class MagnetDemoPage : ContentPage
{
    public MagnetDemoPage(MagnetDemoPageModel magnetDemoPageModel)
    {
        BindingContext = magnetDemoPageModel;
        InitializeComponent();
    }

    private void ToggleCardDetail(object? sender, TappedEventArgs e)
    {
        CardDetail.IsVisible = !CardDetail.IsVisible;
    }
}
