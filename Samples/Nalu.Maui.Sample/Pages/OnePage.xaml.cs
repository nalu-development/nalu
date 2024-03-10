namespace Nalu.Maui.Sample.Pages;

using OnePageModel = PageModels.OnePageModel;

public partial class OnePage
{
    public OnePage(OnePageModel onePageModel)
    {
        BindingContext = onePageModel;
        InitializeComponent();
    }
}
