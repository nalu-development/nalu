namespace Nalu.Maui.DailyHelper.Overlays;

public partial class DurationSheetView : ContentView
{
    public DurationSheetView(DurationSheetModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
