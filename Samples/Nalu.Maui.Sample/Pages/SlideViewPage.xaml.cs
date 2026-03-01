using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.Pages;

public class SlideViewPageModel : ObservableObject;
public partial class SlideViewPage : ContentPage
{
    public SlideViewPage(SlideViewPageModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }

    private void OnToggleOrientationClicked(object? sender, EventArgs e)
    {
        SlideView.Orientation = SlideView.Orientation == StackOrientation.Horizontal
            ? StackOrientation.Vertical
            : StackOrientation.Horizontal;
    }
}

