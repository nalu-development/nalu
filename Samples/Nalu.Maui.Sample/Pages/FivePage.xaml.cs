using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class FivePage : ContentPage
{
    public FivePage(FivePageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }

    private void ScrollToMe(object? sender, EventArgs e)
    {
        TheCollectionView.ScrollTo((sender as BindableObject)?.BindingContext);
    }
}
