namespace Nalu;

internal interface IShellProxy
{
    IShellItemProxy CurrentItem { get; }
    IReadOnlyList<IShellItemProxy> Items { get; }
    Color GetForegroundColor(Page page);
    IShellContentProxy GetContent(string segmentName);
    Task SelectContentAsync(string segmentName);
    Task PushAsync(string segmentName, Page page);
    Task PopAsync(IShellSectionProxy section);
}
