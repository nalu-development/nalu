namespace Nalu.Maui.Weather.Pages;

using PageModels;

public partial class InitializationPage
{
    public InitializationPage(InitializationPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}

