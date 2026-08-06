using Nalu.Maui.DailyHelper.PageModels;

namespace Nalu.Maui.DailyHelper.Pages;

public partial class TasksPage : ContentPage
{
    /// <summary>Typed accessor for x:Reference bindings inside virtualized cell templates.</summary>
    public TasksPageModel Model { get; }

    public TasksPage(TasksPageModel model)
    {
        Model = model;
        BindingContext = model;
        InitializeComponent();
    }
}
