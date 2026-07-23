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
    internal async void ResetToMainPage()
    {
        // Close any modal pages (e.g. popups left open by a failed test) before swapping the page.
        var currentPage = Windows[0].Page;
        var navigation = currentPage?.Navigation;

        while (navigation?.ModalStack.Count > 0)
        {
            await navigation.PopModalAsync(false);
        }

        Windows[0].Page = new MainPage(_serviceProvider);

        // Tear down disposable pages (e.g. NaluShell): leaving them wired would deliver
        // zombie lifecycle events and break the next shell instance's navigation.
        (currentPage as IDisposable)?.Dispose();
    }
}
