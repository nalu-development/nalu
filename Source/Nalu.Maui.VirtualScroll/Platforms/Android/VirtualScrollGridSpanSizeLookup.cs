using AndroidX.RecyclerView.Widget;

namespace Nalu;

/// <summary>
/// Maps flattened adapter positions onto grid lines: headers and footers take a whole line, and
/// every section starts on a fresh one.
/// </summary>
/// <remarks>
/// <para>
/// The three overrides have to agree with each other, which is why none of them can be left to the
/// base class. The default implementations pack positions end to end — a section whose item count
/// is not a multiple of the span would leak its trailing gap into the next section, placing that
/// section's first item mid-line.
/// </para>
/// <para>
/// Both AndroidX caches are enabled: <see cref="GetSpanGroupIndex" /> is linear in the number of
/// sections, and the decoration asks for it once per laid-out cell.
/// </para>
/// </remarks>
internal sealed class VirtualScrollGridSpanSizeLookup : GridLayoutManager.SpanSizeLookup
{
    private readonly Func<IVirtualScrollFlattenedAdapter?> _flattenedAdapterProvider;
    private readonly Func<IVirtualScroll?> _virtualScrollProvider;

    public VirtualScrollGridSpanSizeLookup(
        Func<IVirtualScrollFlattenedAdapter?> flattenedAdapterProvider,
        Func<IVirtualScroll?> virtualScrollProvider)
    {
        _flattenedAdapterProvider = flattenedAdapterProvider;
        _virtualScrollProvider = virtualScrollProvider;

        SpanIndexCacheEnabled = true;
        SpanGroupIndexCacheEnabled = true;
    }

    public override int GetSpanSize(int position)
    {
        var spanCount = CurrentSpanCount;

        if (_flattenedAdapterProvider() is not { } adapter || !adapter.TryGetPositionInfo(position, out var positionType, out _))
        {
            return spanCount;
        }

        // Anything that is not an item — global header/footer, section header/footer — is a line of its own.
        return positionType == VirtualScrollFlattenedPositionType.Item ? 1 : spanCount;
    }

    public override int GetSpanIndex(int position, int spanCount)
    {
        if (_flattenedAdapterProvider() is not { } adapter || !adapter.TryGetSectionAndItemIndex(position, out _, out var itemIndex))
        {
            // Full-span positions always start their line.
            return 0;
        }

        // The item index restarts at 0 for every section, so the column follows from it directly:
        // this is what makes a section begin on a new line without any extra bookkeeping.
        return itemIndex % spanCount;
    }

    public override int GetSpanGroupIndex(int adapterPosition, int spanCount)
    {
        if (_flattenedAdapterProvider() is not { } flattenedAdapter
            || _virtualScrollProvider() is not { Adapter: { } adapter } virtualScroll
            || virtualScroll is not IVirtualScrollLayoutInfo layoutInfo
            || !flattenedAdapter.TryGetPositionInfo(adapterPosition, out var positionType, out var sectionIndex))
        {
            return 0;
        }

        if (positionType == VirtualScrollFlattenedPositionType.GlobalHeader)
        {
            return 0;
        }

        var lines = layoutInfo.HasGlobalHeader ? 1 : 0;
        var sectionHeaderLines = layoutInfo.HasSectionHeader ? 1 : 0;
        var sectionFooterLines = layoutInfo.HasSectionFooter ? 1 : 0;

        if (positionType == VirtualScrollFlattenedPositionType.GlobalFooter)
        {
            var sectionCount = adapter.GetSectionCount();

            for (var section = 0; section < sectionCount; section++)
            {
                lines += sectionHeaderLines + LineCount(adapter.GetItemCount(section), spanCount) + sectionFooterLines;
            }

            return lines;
        }

        for (var section = 0; section < sectionIndex; section++)
        {
            lines += sectionHeaderLines + LineCount(adapter.GetItemCount(section), spanCount) + sectionFooterLines;
        }

        if (positionType == VirtualScrollFlattenedPositionType.SectionHeader)
        {
            return lines;
        }

        lines += sectionHeaderLines;

        if (positionType == VirtualScrollFlattenedPositionType.SectionFooter)
        {
            return lines + LineCount(adapter.GetItemCount(sectionIndex), spanCount);
        }

        return flattenedAdapter.TryGetSectionAndItemIndex(adapterPosition, out _, out var itemIndex)
            ? lines + (itemIndex / spanCount)
            : lines;
    }

    /// <summary>
    /// The span count is not passed to <see cref="GetSpanSize" />, so the layout manager keeps it here.
    /// </summary>
    public int CurrentSpanCount { get; set; } = 1;

    private static int LineCount(int itemCount, int spanCount) => (itemCount + spanCount - 1) / spanCount;
}
