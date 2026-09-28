namespace Nalu;

/// <summary>
/// A grid virtual scroll layout that scrolls vertically, arranging items in rows of
/// <see cref="GridVirtualScrollLayout.Span" /> columns.
/// </summary>
public sealed class VerticalGridVirtualScrollLayout() : GridVirtualScrollLayout(ItemsLayoutOrientation.Vertical);
