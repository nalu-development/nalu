using Nalu.Maui.DailyHelper.PageModels;

namespace Nalu.Maui.DailyHelper.Pages;

public partial class WeatherDetailPage : ContentPage
{
    public WeatherDetailPage(WeatherDetailPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
