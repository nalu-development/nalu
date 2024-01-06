namespace Nalu.Maui.Sample.Pages;

using PageModels;

public partial class ThreePage : ContentPage
{
    public ThreePage(ThreePageModel threePageModel)
    {
        BindingContext = threePageModel;
        InitializeComponent();
    }
}

