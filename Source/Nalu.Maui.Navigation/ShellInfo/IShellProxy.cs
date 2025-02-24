namespace Nalu;

internal interface IShellProxy
{
    string State { get; }
    bool BeginNavigation();
    Task CommitNavigationAsync(Action? completeAction = null);
    IShellItemProxy CurrentItem { get; }
    IReadOnlyList<IShellItemProxy> Items { get; }
    Color GetToolbarIconColor(Page page);
    IShellContentProxy GetContent(string segmentName);
    Task SelectContentAsync(string segmentName);
    void InitializeWithContent(string segmentName);
    Task PushAsync(string segmentName, Page page);
    Task PopAsync(IShellSectionProxy section);
    void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args);
}
