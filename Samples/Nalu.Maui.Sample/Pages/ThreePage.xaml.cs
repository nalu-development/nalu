using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

using ThreePageModel = ThreePageModel;

public partial class ThreePage : ContentPage
{
    public ThreePage(ThreePageModel threePageModel)
    {
        BindingContext = threePageModel;
        InitializeComponent();
    }
}
