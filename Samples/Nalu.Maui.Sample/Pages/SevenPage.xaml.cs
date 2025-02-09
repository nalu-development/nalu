using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class SevenPage : ContentPage
{
    public SevenPage(SevenPageModel pageModel)
    {
        BindingContext = pageModel;
        InitializeComponent();
    }
}
