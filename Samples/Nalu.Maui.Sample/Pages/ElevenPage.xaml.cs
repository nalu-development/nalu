using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

public partial class ElevenPage : ContentPage
{
    public ElevenPage(ElevenPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

