namespace Nalu;

/// <summary>
/// Event arguments for the <see cref="ViewBoxBase.ContentChanged" /> event.
/// </summary>
public class ViewBoxContentChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the old content.
    /// </summary>
    public IView? OldContent { get; }

    /// <summary>
    /// Gets the new content.
    /// </summary>
    public IView? NewContent { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ViewBoxContentChangedEventArgs" />.
    /// </summary>
    public ViewBoxContentChangedEventArgs(IView? oldContent, IView? newContent)
    {
        NewContent = newContent;
        OldContent = oldContent;
    }
}
