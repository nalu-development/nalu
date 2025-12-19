namespace Nalu;

/// <summary>
/// Event arguments for the refresh event.
/// </summary>
public class RefreshEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshEventArgs"/> class.
    /// </summary>
    /// <param name="completionCallback">Callback to invoke when the refresh is complete.</param>
    public RefreshEventArgs(Action completionCallback)
    {
        Complete = completionCallback;
    }

    /// <summary>
    /// Gets the callback to invoke when the refresh is complete.
    /// </summary>
    public Action Complete { get; }
}

