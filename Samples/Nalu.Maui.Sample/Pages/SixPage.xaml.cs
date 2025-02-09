using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class SixPage : ContentPage
{
    public SixPage(SixPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}
