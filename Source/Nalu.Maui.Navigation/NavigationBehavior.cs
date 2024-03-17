namespace Nalu;

/// <summary>
/// Defines the navigation behavior.
/// </summary>
[Flags]
public enum NavigationBehavior
{
    /// <summary>
    /// Does not touch navigation stacks and shell contents while navigating between them and <see cref="ILeavingGuard"/>s are not ignored.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// When switching to a different <see cref="ShellSection"/> within the same <see cref="ShellItem"/>,
    /// the current navigation stack and <see cref="ShellContent"/>s will be cleared.
    /// </summary>
    /// <remarks>
    /// Does not affect relative navigation.
    /// </remarks>
    PopAllPagesOnSectionChange = 0x01,

    /// <summary>
    /// When switching to a different <see cref="ShellItem"/>, the current navigation stack and <see cref="ShellSection"/>'s
    /// <see cref="ShellContent"/>s will be cleared.
    /// </summary>
    /// <remarks>
    /// Does not affect relative navigation.
    /// </remarks>
    PopAllPagesOnItemChange = 0x02,

    /// <summary>
    /// When popping a page, the <see cref="ILeavingGuard"/>s will be ignored.
    /// </summary>
    IgnoreGuards = 0x04,
}
