using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class TwelvePage : ContentPage
{
    public TwelvePage(TwelvePageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

