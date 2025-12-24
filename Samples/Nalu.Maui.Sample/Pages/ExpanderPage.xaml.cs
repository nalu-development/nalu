using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class ExpanderPage : ContentPage
{
    public ExpanderPage(ExpanderPageModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}

