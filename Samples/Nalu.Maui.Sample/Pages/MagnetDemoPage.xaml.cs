using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class MagnetDemoPage : ContentPage
{
    public MagnetDemoPage(MagnetDemoPageModel magnetDemoPageModel)
    {
        BindingContext = magnetDemoPageModel;
        InitializeComponent();
    }

    private void Button_OnClicked(object? sender, EventArgs e)
    {
        var btn = (Button)sender;
        btn.Text = btn.Text == "Short" ? "Very long text" : "Short";
    }
}
