namespace Nalu.Maui.Sample.Pages;

using ThreePageModel = PageModels.ThreePageModel;

public partial class ThreePage : ContentPage
{
    public ThreePage(ThreePageModel threePageModel)
    {
        BindingContext = threePageModel;
        InitializeComponent();
    }
}

