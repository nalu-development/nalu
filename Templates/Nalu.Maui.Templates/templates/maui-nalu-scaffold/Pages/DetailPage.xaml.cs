using MauiNaluApp.PageModels;

namespace MauiNaluApp.Pages;

public partial class DetailPage : ContentPage
{
    public DetailPage(DetailPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
