using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace Nalu;

/// <summary>
/// Empty adapter implementation for when there's no adapter.
/// </summary>
internal class EmptyVirtualScrollRecyclerViewAdapter : RecyclerView.Adapter
{
    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position) => throw new NotImplementedException();

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) => throw new NotImplementedException();

    public override int ItemCount => 0;
}

