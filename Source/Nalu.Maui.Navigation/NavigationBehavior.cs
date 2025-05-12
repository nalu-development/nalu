namespace Nalu;

/// <summary>
/// Defines the navigation behavior.
/// </summary>
[Flags]
public enum NavigationBehavior
{
    /// <summary>
    /// Does not touch navigation stacks and shell contents while navigating between them and <see cref="ILeavingGuard" />s are not ignored.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// When switching to a different <see cref="ShellSection" /> within the same <see cref="ShellItem" />,
    /// the current navigation stack and <see cref="ShellContent" />s will be cleared.
    /// </summary>
    /// <remarks>
    /// Does not affect relative navigation.
    /// Using this flag alone will not pop pages when switching to a different <see cref="ShellItem" />.
    /// </remarks>
    PopAllPagesOnSectionChange = 0x01,

    /// <summary>
    /// When switching to a different <see cref="ShellItem" />, the current navigation stack and <see cref="ShellSection" />'s
    /// <see cref="ShellContent" />s will be cleared.
    /// </summary>
    /// <remarks>
    /// Does not affect relative navigation.
    /// </remarks>
    PopAllPagesOnItemChange = 0x02,

    /// <summary>
    /// When popping a page, the <see cref="ILeavingGuard" />s will be ignored.
    /// </summary>
    /// <remarks>
    /// Using this flag alone will not pop pages when switching to a different <see cref="ShellItem" />,
    /// evaluate using <see cref="DefaultIgnoreGuards" /> instead.
    /// </remarks>
    IgnoreGuards = 0x04,

    /// <summary>
    /// Immediately navigates to the target page without waiting 60 milliseconds.
    /// </summary>
    /// <remarks>
    /// Default behavior is to wait 60 milliseconds before navigating to the target page to let touch be displayed.
    /// Using this flag alone will not pop pages when switching to a different <see cref="ShellItem" />,
    /// evaluate using <see cref="DefaultImmediate" /> instead.
    /// </remarks>
    Immediate = 0x08,

    /// <summary>
    /// Combines <see cref="IgnoreGuards" /> with the default <see cref="PopAllPagesOnItemChange" /> behavior.
    /// </summary>
    DefaultIgnoreGuards = IgnoreGuards | PopAllPagesOnItemChange,

    /// <summary>
    /// Combines <see cref="IgnoreGuards" /> with the default <see cref="PopAllPagesOnItemChange" /> behavior.
    /// </summary>
    DefaultImmediate = Immediate | PopAllPagesOnItemChange,

    /// <summary>
    /// Combines <see cref="Immediate" /> and <see cref="IgnoreGuards" /> with the default <see cref="PopAllPagesOnItemChange" /> behavior.
    /// </summary>
    DefaultImmediateIgnoreGuards = Immediate | IgnoreGuards | PopAllPagesOnItemChange
}
