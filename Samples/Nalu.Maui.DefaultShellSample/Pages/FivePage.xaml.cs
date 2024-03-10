namespace Nalu.Maui.DefaultShellSample.Pages;

using Nalu.Maui.DefaultShellSample.PageModels;

public partial class FivePage : ContentPage
{
    public FivePage(FivePageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}

