namespace Nalu.Maui.Sample.Pages;

using PageModels;

public partial class FivePage : ContentPage
{
    public FivePage(FivePageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}

