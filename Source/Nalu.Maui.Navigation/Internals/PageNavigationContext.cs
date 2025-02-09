namespace Nalu;

internal sealed class PageNavigationContext(IServiceScope serviceScope) : IDisposable
{
    public IServiceScope ServiceScope => serviceScope;
    public bool Entered { get; set; }
    public bool Appeared { get; set; }

    private static readonly BindableProperty _navigationContextProperty = BindableProperty.CreateAttached(
        "PageNavigationContext",
        typeof(PageNavigationContext),
        typeof(PageNavigationContext),
        null
    );

    public static PageNavigationContext Get(Page page)
    {
        var pageNavigationContext = (PageNavigationContext) page.GetValue(_navigationContextProperty);
#pragma warning disable IDE0270
        if (pageNavigationContext is null)
#pragma warning restore IDE0270
        {
            throw new InvalidOperationException("Cannot navigate to a page not created by Nalu navigation.");
        }

        return pageNavigationContext;
    }

    public static void Set(Page page, PageNavigationContext? context) => page.SetValue(_navigationContextProperty, context);

    public static void Dispose(Page page)
    {
        var context = Get(page);
        context.Dispose();
        page.ClearValue(_navigationContextProperty);
    }

    public void Dispose() => serviceScope.Dispose();
}
