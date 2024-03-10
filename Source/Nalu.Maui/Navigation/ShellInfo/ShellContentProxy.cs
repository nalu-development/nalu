namespace Nalu;

using System.Reflection;

#pragma warning disable CS8618

internal class ShellContentProxy(ShellContent content, IShellSectionProxy parent) : IShellContentProxy
{
    private static readonly PropertyInfo _shellContentCacheProperty = typeof(ShellContent).GetProperty("ContentCache", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public ShellContent Content => content;
    public string SegmentName => content.Route;
    public bool HasGuard => Page?.BindingContext is ILeavingGuard;
    public IShellSectionProxy Parent { get; } = parent;
    public Page? Page => ((IShellContentController)content).Page;
    public Page GetOrCreateContent() => ((IShellContentController)content).GetOrCreateContent();
    public void DestroyContent()
    {
        if (Page is not { } page)
        {
            return;
        }

        PageNavigationContext.Dispose(page);
        _shellContentCacheProperty.SetValue(content, null);
    }
}
