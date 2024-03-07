namespace Nalu.Maui.Sample.Pages;

using Nalu.Maui.Sample.PageModels;

public partial class SevenPage : ContentPage
{
    public SevenPage(SevenPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}

