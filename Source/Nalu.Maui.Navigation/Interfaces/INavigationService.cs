namespace Nalu;

/// <summary>
/// Provides shell navigation abstraction.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the specified model-name-driven path.
    /// </summary>
    /// <param name="navigation">The navigation to apply.</param>
    /// <returns>True if navigation succeeds, false if it was interrupted by guards.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="navigation"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Throw if target path is not reachable or target page model can't receive specified intent.</exception>
    Task<bool> GoToAsync(INavigationInfo navigation);
}
