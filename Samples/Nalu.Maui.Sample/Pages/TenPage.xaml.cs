using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class TenPage : ContentPage
{
    public TenPage(TenPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

