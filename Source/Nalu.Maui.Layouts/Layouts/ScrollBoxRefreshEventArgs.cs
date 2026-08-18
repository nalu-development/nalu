namespace Nalu;

/// <summary>
/// Event arguments for the <see cref="ScrollBox.OnRefresh" /> event.
/// </summary>
/// <remarks>
/// Named with the <c>ScrollBox</c> prefix (instead of reusing <c>RefreshEventArgs</c>) so apps
/// referencing both Nalu.Maui.Layouts and Nalu.Maui.VirtualScroll never face an ambiguous type in
/// the shared <c>Nalu</c> namespace.
/// </remarks>
public class ScrollBoxRefreshEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScrollBoxRefreshEventArgs" /> class.
    /// </summary>
    /// <param name="completionCallback">Callback to invoke when the refresh is complete.</param>
    public ScrollBoxRefreshEventArgs(Action completionCallback)
    {
        Complete = completionCallback;
    }

    /// <summary>
    /// Gets the callback to invoke when the refresh is complete.
    /// </summary>
    public Action Complete { get; }
}
