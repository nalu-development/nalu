using Android.Content;
using Android.Runtime;
using Android.Util;
using AndroidX.RecyclerView.Widget;

namespace Nalu;

public class VirtualScrollRecyclerView : RecyclerView
{
    public ItemsLayoutOrientation Orientation { get; set; } = ItemsLayoutOrientation.Vertical;
    
    protected VirtualScrollRecyclerView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public VirtualScrollRecyclerView(Context context) : base(context)
    {
    }

    public VirtualScrollRecyclerView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
    }

    public VirtualScrollRecyclerView(Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
    {
    }
}
