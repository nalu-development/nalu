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
    internal void ResetToMainPage()
    {
        var currentPage = Windows[0].Page;

        Windows[0].Page = new MainPage(_serviceProvider);

        // Tear down disposable pages (e.g. NaluShell): leaving them wired would deliver
        // zombie lifecycle events and break the next shell instance's navigation.
        // Draining: pages disposed by the shell teardown die with the shell graph, which
        // MAUI 10 iOS retains after the swap (see LeakTracker remarks) — don't assert them.
        LeakTracker.Draining = true;

        try
        {
            (currentPage as IDisposable)?.Dispose();

            // MAUI does not reliably disconnect the old page's handler tree when Window.Page
            // is swapped; without this the native view hierarchy keeps the page graph alive.
            // This is also what retires whatever the outgoing page still had presented — modals
            // and popups included. Popping them one by one first would be both redundant and
            // wrong: a Scaffold holds several navigation stacks at once, and only one of them
            // is the window's Navigation.
            currentPage?.DisconnectHandlers();
        }
        finally
        {
            LeakTracker.Draining = false;
        }
    }
}
