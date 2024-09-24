namespace Nalu;

/// <summary>
/// Provides Nalu navigation configuration.
/// </summary>
public interface INavigationConfiguration
{
    /// <summary>
    /// Gets the image used to display a navigation menu button on root pages.
    /// </summary>
    ImageSource? MenuImage { get; }

    /// <summary>
    /// Gets the image used to display a navigation back button on nested pages.
    /// </summary>
    ImageSource? BackImage { get; }

    /// <summary>
    /// Gets a dictionary which maps a page model type to corresponding page type.
    /// </summary>
    IReadOnlyDictionary<Type, Type> Mapping { get; }

    /// <summary>
    /// Gets the navigation leak detector state.
    /// </summary>
    NavigationLeakDetectorState LeakDetectorState { get; }
}
