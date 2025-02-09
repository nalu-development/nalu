namespace Nalu;

/// <summary>
/// <see cref="INavigationService" /> will invoke <see cref="OnAppearingAsync" /> method when the page is appearing
/// and no navigation intent has been provided.
/// </summary>
public interface IAppearingAware
{
    /// <summary>
    /// Invoked when the page is appearing without a navigation intent.
    /// </summary>
    /// <returns>A task which completes when appearing routines are completed.</returns>
    ValueTask OnAppearingAsync();
}

/// <summary>
/// <see cref="INavigationService" /> will invoke <see cref="OnAppearingAsync" /> method when the page is appearing
/// and a <typeparamref name="TIntent" /> navigation intent has been provided.
/// </summary>
/// <typeparam name="TIntent">The type of supported navigation intent.</typeparam>
public interface IAppearingAware<in TIntent>
{
    /// <summary>
    /// Invoked when the page is appearing with a <typeparamref name="TIntent" /> navigation intent.
    /// </summary>
    /// <param name="intent">The navigation intent.</param>
    /// <returns>A task which completes when appearing routines are completed.</returns>
    ValueTask OnAppearingAsync(TIntent intent);
}
