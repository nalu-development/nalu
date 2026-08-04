using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace Nalu;

/// <summary>
/// Placeholder adapter mounted while no data source is attached: the Java-side cached count
/// defaults to zero, so RecyclerView's frequent getItemCount reads never cross into managed
/// code, and the create/bind callbacks are unreachable.
/// </summary>
internal class EmptyVirtualScrollRecyclerViewAdapter : Platform.VirtualScrollNativeAdapter
{
    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position) => throw new NotImplementedException();

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) => throw new NotImplementedException();
}
