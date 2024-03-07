namespace Nalu.Maui.Sample.Pages;

using PageModels;
using TwoPageModel = Sample.PageModels.TwoPageModel;

public partial class TwoPage : ContentPage
{
    public TwoPage(TwoPageModel enchantedPageModel)
    {
        BindingContext = enchantedPageModel;
        InitializeComponent();
    }
}

