using Android.Content;
using Android.Runtime;
using Android.Util;
using Nalu.Platform;

namespace Nalu;

/// <summary>
/// The VirtualScroll platform scrollable. Hot-path logic lives in the Java base class
/// <see cref="VirtualScrollNativeRecyclerView"/> — fading-edge padding offsets (per-frame
/// draw path), focus tracking + orphaned-IME handling (per-recycle detach path), and the
/// positional safe-area self-padding incl. window-insets consumption (per-layout path) —
/// so those framework callbacks never cross the JNI boundary; this managed side keeps the
/// MAUI integration (scroll adjustment, adapter wiring).
/// </summary>
public class VirtualScrollRecyclerView : VirtualScrollNativeRecyclerView
{
    private VirtualScrollRecyclerViewScrollHelper? _scrollHelper;
    public ItemsLayoutOrientation Orientation { get; set; } = ItemsLayoutOrientation.Vertical;
    public Action? OnLayoutCallback { get; set; }
    internal VirtualScrollRecyclerViewScrollHelper ScrollHelper => _scrollHelper ??= new VirtualScrollRecyclerViewScrollHelper(this);

    public VirtualScrollRecyclerView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public VirtualScrollRecyclerView(Context context) : base(context)
    {
        // Clip flags, the insets listener and the self-padding model are all set up by the
        // native base ctor.
    }

    public VirtualScrollRecyclerView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
    }

    public VirtualScrollRecyclerView(Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
    {
    }

    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        base.OnLayout(changed, l, t, r, b);
        // After a direct (non-animated) scroll operation, we may need to make adjustments
        // to align the target item; if an adjustment is pending, execute it here.
        // (Deliberately checking the private member here rather than the property accessor; the accessor will
        // create a new ScrollHelper if needed, and there's no reason to do that until a Scroll is requested.)
        _scrollHelper?.AdjustScroll();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scrollHelper?.Dispose();
            _scrollHelper = null;
        }

        base.Dispose(disposing);
    }
}
