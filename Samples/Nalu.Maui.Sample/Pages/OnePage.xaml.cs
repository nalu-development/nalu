using Nalu.Maui.Sample.PageModels;

namespace Nalu.Maui.Sample.Pages;

using OnePageModel = OnePageModel;

public partial class OnePage
{
    public OnePage(OnePageModel onePageModel)
    {
        BindingContext = onePageModel;
        InitializeComponent();
    }
}
