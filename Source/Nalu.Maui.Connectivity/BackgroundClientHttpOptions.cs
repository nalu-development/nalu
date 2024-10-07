namespace Nalu;

/// <summary>
/// Provides options for the background HTTP client.
/// </summary>
public class BackgroundClientHttpOptions
{
    /// <summary>
    /// Gets or sets the default user notification title.
    /// </summary>
    public string DefaultUserTitle { get; set; } = AppInfo.Name;

    /// <summary>
    /// Gets or sets the default user notification description.
    /// </summary>
    public string DefaultUserDescription { get; set; } = "Syncing data in the background";
}
