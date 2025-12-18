using Android.Runtime;
using AndroidX.RecyclerView.Widget;

namespace Nalu;

internal class VirtualScrollViewHolder : RecyclerView.ViewHolder
{
    public VirtualScrollViewHolder(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public VirtualScrollViewHolder(VirtualScrollViewWrapper itemView) : base(itemView)
    {
    }

    public VirtualScrollViewWrapper ViewWrapper => (VirtualScrollViewWrapper)ItemView;
}
