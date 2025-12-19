namespace Nalu;

/// <summary>
/// Controller interface for VirtualScroll refresh functionality.
/// </summary>
public interface IVirtualScrollController
{
    /// <summary>
    /// Refreshes the content. Called by the platform when the user triggers a refresh.
    /// </summary>
    /// <param name="completionCallback">Callback to invoke when the refresh is complete.</param>
    void Refresh(Action completionCallback);
}

