namespace Nalu.Maui.DefaultShellSample.Pages;

using Nalu.Maui.DefaultShellSample.PageModels;

public partial class SixPage : ContentPage
{
    public SixPage(SixPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}

