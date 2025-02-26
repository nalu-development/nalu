namespace Nalu;

/// <summary>
/// Exposes the navigation which is about to happen and gives the ability to cancel it.
/// </summary>
public class NaluShellNavigatingEventArgs
{
    /// <summary>
    /// The navigation which is about to happen.
    /// </summary>
    public required INavigationInfo Navigation { get; init; }

    /// <summary>
    /// Cancels the navigation.
    /// </summary>
    public void Cancel() => Canceled = true;

    /// <summary>
    /// Whether this navigation should be canceled.
    /// </summary>
    internal bool Canceled { get; private set; }
}
