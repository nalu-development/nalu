namespace Nalu;

internal enum NavigationLevel
{
    /// <summary>
    /// Navigation targeting the current <see cref="ShellSection"/>.
    /// </summary>
    Section,

    /// <summary>
    /// Navigation targeting the current <see cref="ShellItem"/>.
    /// </summary>
    Item,

    /// <summary>
    /// Navigation targeting a <see cref="ShellItem"/> different from the current one.
    /// </summary>
    Shell,
}
