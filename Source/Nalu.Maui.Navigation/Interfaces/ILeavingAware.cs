namespace Nalu;

/// <summary>
/// <see cref="INavigationService"/> will invoke <see cref="OnLeavingAsync"/> method when the page is about to
/// being removed from the navigation stack.
/// </summary>
public interface ILeavingAware
{
    /// <summary>
    /// Invoked when the page is about to being removed from the navigation stack.
    /// </summary>
    ValueTask OnLeavingAsync();
}
