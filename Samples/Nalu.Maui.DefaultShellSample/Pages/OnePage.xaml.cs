namespace Nalu.Maui.Sample.Pages;

using PageModels;

public partial class OnePage
{
    public OnePage(OnePageModel onePageModel)
    {
        BindingContext = onePageModel;
        InitializeComponent();
    }
}
