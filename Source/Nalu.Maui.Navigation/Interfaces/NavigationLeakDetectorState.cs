namespace Nalu;

/// <summary>
/// Defines the navigation leak detector state.
/// </summary>
public enum NavigationLeakDetectorState
{
    /// <summary>
    /// Leak detector is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Leak detector is enabled only when debugger is attached.
    /// </summary>
    EnabledWithDebugger,

    /// <summary>
    /// Leak detector is always enabled.
    /// </summary>
    Enabled
}
