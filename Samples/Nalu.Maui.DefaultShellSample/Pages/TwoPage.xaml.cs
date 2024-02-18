namespace Nalu.Maui.Sample.Pages;

using PageModels;

public partial class TwoPage : ContentPage
{
    public TwoPage(TwoPageModel enchantedPageModel)
    {
        BindingContext = enchantedPageModel;
        InitializeComponent();
    }
}

