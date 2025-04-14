using Nalu.Maui.Weather.PageModels;

namespace Nalu.Maui.Weather.Pages;

public partial class SettingsPage
{
    public SettingsPage(SettingsPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
