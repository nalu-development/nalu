namespace Nalu.Maui.Sample.Pages;

using PageModels;

public partial class FourPage : ContentPage
{
    public FourPage(FourPageModel fourPageModel)
    {
        BindingContext = fourPageModel;
        InitializeComponent();
    }
}

