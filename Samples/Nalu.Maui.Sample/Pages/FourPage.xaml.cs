using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

using FourPageModel = FourPageModel;

public partial class FourPage : ContentPage
{
    public FourPage(FourPageModel fourPageModel)
    {
        BindingContext = fourPageModel;
        InitializeComponent();
    }
}
