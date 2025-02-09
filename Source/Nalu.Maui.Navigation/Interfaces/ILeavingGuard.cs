namespace Nalu;

/// <summary>
/// <see cref="INavigationService" /> will invoke <see cref="CanLeaveAsync" /> method when the page is requested to
/// be removed from the navigation stack.
/// </summary>
public interface ILeavingGuard
{
    /// <summary>
    /// Invoked when the page is requested to be removed from the navigation stack.
    /// </summary>
    /// <returns>True to let the page leave, false to stop the navigation.</returns>
    ValueTask<bool> CanLeaveAsync();
}
