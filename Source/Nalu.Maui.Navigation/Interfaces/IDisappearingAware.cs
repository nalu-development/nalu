namespace Nalu;

/// <summary>
/// <see cref="INavigationService" /> will invoke <see cref="OnDisappearingAsync" /> method when the page is
/// disappearing due to another page being pushed onto the navigation stack.
/// </summary>
public interface IDisappearingAware
{
    /// <summary>
    /// Invoked when the page is disappearing due to another page being pushed onto the navigation stack.
    /// </summary>
    ValueTask OnDisappearingAsync();
}
