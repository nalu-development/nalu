namespace Nalu.Maui.Weather.Pages;

using PageModels;
using Resources;

public partial class HomePage
{
    public HomePage(HomePageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }

    private void ShowMoreWeatherForecast(object? sender, EventArgs e)
    {
        WeatherExpander.IsExpanded = !WeatherExpander.IsExpanded;
        WeatherExpanderButton.Text = WeatherExpander.IsExpanded ? Texts.ShowLess : Texts.ShowMore;
    }

    private void ShowMoreAirQuality(object? sender, EventArgs e)
    {
        AirQualityExpander.IsExpanded = !AirQualityExpander.IsExpanded;
        AirQualityExpanderButton.Text = AirQualityExpander.IsExpanded ? Texts.ShowLess : Texts.ShowMore;
    }
}

