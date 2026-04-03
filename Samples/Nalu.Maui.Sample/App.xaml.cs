using Nalu.Maui.Sample.PageModels;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;

namespace Nalu.Maui.Sample;

public partial class App : Application
{
    private readonly INavigationService _navigationService;
#if ANDROID
    private Window? _window;
#endif

    public App(INavigationService navigationService)
    {
        _navigationService = navigationService;
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
        InitializeComponent();
    }

    private async void OnNotificationActionTapped(NotificationActionEventArgs e)
    {
        if (!e.IsTapped || e.Request.NotificationId != LocalNotificationIds.FiveRootThenThreeStack)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
            await _navigationService.GoToAsync(Navigation.Absolute().Root<FivePageModel>().Add<ThreePageModel>())
        );
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if ANDROID
        // We need to cache the window instance to prevent the app from creating a new window
        // when foregrounding the app from the background
        return _window ?? new Window(new AppShell(_navigationService));
#else
        return new Window(new AppShell(_navigationService));
#endif
    }
    
    
}
