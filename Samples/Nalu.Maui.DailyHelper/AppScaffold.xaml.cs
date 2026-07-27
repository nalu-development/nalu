using DynamicData;
using DynamicData.Aggregation;
using Nalu.Maui.DailyHelper.Services;

namespace Nalu.Maui.DailyHelper;

public partial class AppScaffold : Scaffold
{
    private readonly IDisposable _badgeSubscription;

    public AppScaffold(TodoStore todos)
    {
        InitializeComponent();

        // Centralized state pays off: the tab badge is just another pipeline over the store —
        // complete a task anywhere and the badge updates instantly.
        _badgeSubscription = todos.Connect()
                                  .Filter(t => !t.IsDone)
                                  .Count()
                                  .Subscribe(count => ScaffoldTabBarView.SetBadgeText(TasksRoot, count > 0 ? count.ToString() : null));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _badgeSubscription.Dispose();
        }

        base.Dispose(disposing);
    }
}
