namespace Nalu;

internal interface IShellSectionProxy
{
    string SegmentName { get; }
    IShellContentProxy CurrentContent { get; }
    IReadOnlyList<IShellContentProxy> Contents { get; }
    IShellItemProxy Parent { get; }
    IEnumerable<NavigationStackPage> GetNavigationStack(IShellContentProxy? content = null);
    Task PopAsync();
    void RemoveStackPages();
}
