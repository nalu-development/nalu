namespace Nalu.Maui.TestApp;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

#if ANDROID
    private Window? _window;
#endif

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if ANDROID
        // We need to cache the window instance to prevent the app from creating a new window
        // when foregrounding the app from the background
        return _window ?? new Window(new MainPage(_serviceProvider));
#else
        return new Window(new MainPage(_serviceProvider));
#endif
    }
}
