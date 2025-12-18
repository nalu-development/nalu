namespace Nalu;


internal class VirtualScrollFlattenedAdapter : IVirtualScrollFlattenedAdapter
{
    private readonly IVirtualScrollAdapter _virtualScrollAdapter;
    private IVirtualScrollLayoutInfo _layoutInfo;

    public VirtualScrollFlattenedAdapter(IVirtualScrollAdapter virtualScrollAdapter, IVirtualScrollLayoutInfo layoutInfo)
    {
        _virtualScrollAdapter = virtualScrollAdapter;
        _layoutInfo = layoutInfo;
    }


    /// <summary>
    /// Sets the layout header / footer information.
    /// </summary>
    public void ChangeLayoutInfo(IVirtualScrollLayoutInfo layoutInfo)
    {
        // TODO: Handle changes in layout info if any
        _ = layoutInfo;
        // Once done update the layout info
        _layoutInfo = layoutInfo;
    }

    public int GetItemCount() => throw new NotImplementedException();

    public VirtualScrollFlattenedPositionInfo GetItem(int flattenedIndex) => throw new NotImplementedException();

    public IDisposable Subscribe(Action<VirtualScrollFlattenedChangeSet> changeCallback) => throw new NotImplementedException();
}
