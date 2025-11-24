using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class NinePage : ContentPage
{
    public NinePage(NinePageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

