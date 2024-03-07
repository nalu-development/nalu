namespace Nalu.Maui.Sample.Pages;

using FourPageModel = PageModels.FourPageModel;

public partial class FourPage : ContentPage
{
    public FourPage(FourPageModel fourPageModel)
    {
        BindingContext = fourPageModel;
        InitializeComponent();
    }
}

