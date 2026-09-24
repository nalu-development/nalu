namespace Nalu;

/// <summary>
/// A grid virtual scroll layout that scrolls horizontally, arranging items in columns of
/// <see cref="GridVirtualScrollLayout.Span" /> rows.
/// </summary>
public sealed class HorizontalGridVirtualScrollLayout() : GridVirtualScrollLayout(ItemsLayoutOrientation.Horizontal);
