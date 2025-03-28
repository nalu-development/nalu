using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

using TwoPageModel = TwoPageModel;

public partial class TwoPage : ContentPage
{
    public TwoPage(TwoPageModel enchantedPageModel)
    {
        BindingContext = enchantedPageModel;
        InitializeComponent();
        DurationWheel.WholeDuration = TimeSpan.FromMinutes(5);
    }
}
