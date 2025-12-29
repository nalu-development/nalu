using System.Diagnostics;
using System.Text.Json;
using Nalu.Maui.Sample.Pages;

namespace Nalu.Maui.Sample;

public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService)
        : base(navigationService, typeof(OnePage))
    {
        InitializeComponent();
        CustomMenuItem.Command = new Command(() =>
        {
            navigationService.GoToAsync(Nalu.Navigation.Relative().Push<SixPage>());
            FlyoutIsPresented = false;
        });
        NavigationEvent += OnNavigationEvent;
    }

    private void OnNavigationEvent(object? sender, NavigationLifecycleEventArgs e)
    {
        if (e.Target is NavigationLifecycleInfo info)
        {
            Debug.WriteLine($"{e.EventType}: {JsonSerializer.Serialize(new { info.RequestedNavigation, info.TargetState, info.CurrentState })}");

            return;
        }

        Debug.WriteLine($"{e.Target.GetType().Name}.{e.EventType}: {JsonSerializer.Serialize(new { Handling = e.Handling.ToString(), Intent = e.Data?.GetType().Name })}");
    }
}
