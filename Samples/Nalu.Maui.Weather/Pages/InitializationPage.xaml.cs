using Nalu.Maui.Weather.PageModels;

namespace Nalu.Maui.Weather.Pages;

public partial class InitializationPage
{
    public InitializationPage(InitializationPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
