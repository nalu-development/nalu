namespace Nalu.Maui.Weather.Pages;

using PageModels;

public partial class HomePage
{
    public HomePage(HomePageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}

