using MauiNaluApp.PageModels;

namespace MauiNaluApp.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
