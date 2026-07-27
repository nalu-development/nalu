using Nalu.Maui.DailyHelper.PageModels;

namespace Nalu.Maui.DailyHelper.Pages;

public partial class TodayPage : ContentPage
{
    /// <summary>Typed accessor for x:Reference bindings inside virtualized cell templates.</summary>
    public TodayPageModel Model { get; }

    public TodayPage(TodayPageModel model)
    {
        Model = model;
        BindingContext = model;
        InitializeComponent();
    }
}
