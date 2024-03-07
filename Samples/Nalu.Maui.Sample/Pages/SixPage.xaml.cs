namespace Nalu.Maui.Sample.Pages;

using Nalu.Maui.Sample.PageModels;

public partial class SixPage : ContentPage
{
    public SixPage(SixPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}

