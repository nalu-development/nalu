namespace Nalu.Maui.DefaultShellSample.Pages;

using PageModels;
using TwoPageModel = DefaultShellSample.PageModels.TwoPageModel;

public partial class TwoPage : ContentPage
{
    public TwoPage(TwoPageModel enchantedPageModel)
    {
        BindingContext = enchantedPageModel;
        InitializeComponent();
    }
}

