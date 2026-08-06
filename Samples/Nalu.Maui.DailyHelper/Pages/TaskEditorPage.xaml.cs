using Nalu.Maui.DailyHelper.PageModels;

namespace Nalu.Maui.DailyHelper.Pages;

public partial class TaskEditorPage : ContentPage
{
    public TaskEditorPage(TaskEditorPageModel model)
    {
        BindingContext = model;
        InitializeComponent();
    }
}
