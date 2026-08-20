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
        var currentPage = Windows[0].Page;

        // Modals are the one thing DisconnectHandlers below cannot reach: they are presented by
        // the WINDOW, not by the page, so a popup left open by a test outlives the swap and ends
        // up covering the new MainPage. The next test then opens its own popup over the leftover
        // one and closes only half of what it can see — PopupTests fails exactly that way
        // without these lines, and stays broken for every run after it.
        // This is the window's MODAL stack, not a navigation stack: nothing here pops a page
        // from the Scaffold's own stacks, of which there may be several in parallel.
        var navigation = currentPage?.Navigation;

        while (navigation?.ModalStack.Count > 0)
        {
            await navigation.PopModalAsync(false);
        }

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
            currentPage?.DisconnectHandlers();
        }
        finally
        {
            LeakTracker.Draining = false;
        }
    }
}
