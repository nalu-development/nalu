namespace Nalu.Maui.DefaultShellSample.Pages;

using Nalu.Maui.DefaultShellSample.PageModels;

public partial class SevenPage : ContentPage
{
    public SevenPage(SevenPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}

