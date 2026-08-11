using MauiNaluApp.PageModels;

namespace MauiNaluApp.Pages;

public partial class HomePage : ContentPage
{
    public HomePage(HomePageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
