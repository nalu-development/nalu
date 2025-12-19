namespace Nalu;

/// <summary>
/// Arguments for the ScrollTo command.
/// </summary>
/// <param name="SectionIndex">The index of the section to scroll to.</param>
/// <param name="ItemIndex">The index of the item within the section to scroll to.</param>
/// <param name="Position">The position to scroll to. Defaults to <see cref="Microsoft.Maui.Controls.ScrollToPosition.MakeVisible"/>.</param>
/// <param name="Animated">Whether the scroll should be animated. Defaults to <c>true</c>.</param>
public readonly record struct VirtualScrollCommandScrollToArgs(
    int SectionIndex,
    int ItemIndex,
    ScrollToPosition Position = ScrollToPosition.MakeVisible,
    bool Animated = true);

