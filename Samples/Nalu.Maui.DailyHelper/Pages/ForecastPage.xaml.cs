using Nalu.Maui.DailyHelper.PageModels;

namespace Nalu.Maui.DailyHelper.Pages;

public partial class ForecastPage : ContentPage
{
    /// <summary>Typed accessor for x:Reference bindings inside virtualized cell templates.</summary>
    public ForecastPageModel Model { get; }

    public ForecastPage(ForecastPageModel model)
    {
        Model = model;
        BindingContext = model;
        InitializeComponent();
    }

    private void OnNowTapped(object? sender, EventArgs e)
    {
        if (Model.CurrentHour is { } hour)
        {
            Scroll.ScrollTo(hour, ScrollToPosition.Start);
        }
    }
}
