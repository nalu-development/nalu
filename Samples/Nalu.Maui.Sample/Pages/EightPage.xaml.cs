using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class EightPage : ContentPage
{
    public EightPage(EightPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

