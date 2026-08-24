namespace Nalu;

internal sealed class PageNavigationContext : IDisposable
{
    private IServiceScope? _serviceScope;

    public PageNavigationContext(IServiceScope serviceScope)
    {
        _serviceScope = serviceScope;
    }

    public IServiceScope ServiceScope => _serviceScope ?? throw new ObjectDisposedException(nameof(PageNavigationContext));
    public bool Entered { get; set; }
    public bool Appeared { get; set; }
    public IAwaitableIntentController? AwaitableIntentController { get; set; }

    /// <summary>
    /// Set for component-based pages (<see cref="IComponentPageFactory" />): owns the mounted
    /// component tree and redirects the navigation lifecycle target to the component — checked
    /// FIRST by <c>NavigationHelper.GetLifecycleTarget</c>, deliberately bypassing the page's
    /// <c>BindingContext</c> (never assigned for component pages: propagating a context through
    /// a component-rendered tree would be pure overhead).
    /// </summary>
    public IComponentPageHandle? ComponentHandle { get; init; }

    private static readonly BindableProperty _navigationContextProperty = BindableProperty.CreateAttached(
        "PageNavigationContext",
        typeof(PageNavigationContext),
        typeof(PageNavigationContext),
        null
    );

    public static PageNavigationContext Get(Page page)
        => TryGet(page) ?? throw new InvalidOperationException("Cannot navigate to a page not created by Nalu navigation.");

    public static PageNavigationContext? TryGet(Page page) => (PageNavigationContext?) page.GetValue(_navigationContextProperty);

    public static bool HasNavigationContext(Page page) => page.GetValue(_navigationContextProperty) is not null;

    public static void Set(Page page, PageNavigationContext? context) => page.SetValue(_navigationContextProperty, context);

    public static void Dispose(Page page)
    {
        var context = Get(page);
        context.Dispose();
        Set(page, null);
    }

    public void Dispose()
    {
        AwaitableIntentController?.Complete();

        if (_serviceScope is not null)
        {
            // Unmount the component tree BEFORE tearing down the scope: the component may
            // still touch scoped services from its unmount path.
            ComponentHandle?.Dispose();

            _serviceScope.Dispose();
            _serviceScope = null;
        }
    }
}
