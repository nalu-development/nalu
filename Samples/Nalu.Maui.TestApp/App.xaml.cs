namespace Nalu.Maui.TestApp;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    private Window? _window;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window ??= new Window(new MainPage(_serviceProvider));
        return _window;
    }

    /// <summary>
    /// Brings the app back to the test-selection page.
    /// Used by the cross-platform "ResetButton" overlay added to every test page,
    /// so UI tests can reset the app state without restarting it.
    /// </summary>
    internal void ResetToMainPage() => Windows[0].Page = new MainPage(_serviceProvider);
}
