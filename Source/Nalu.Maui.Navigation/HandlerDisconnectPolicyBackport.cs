namespace Nalu;

/// <summary>
/// Backport of the DisconnectPolicy property from .NET9.
/// </summary>
public enum HandlerDisconnectPolicyBackport
{
    /// <summary>
    /// Automatically disconnect the handler when the associated element is removed from the visual tree.
    /// </summary>
    Automatic,

    /// <summary>
    /// Do not automatically disconnect the handler when the associated element is removed from the visual tree.
    /// </summary>
    Manual,
}
