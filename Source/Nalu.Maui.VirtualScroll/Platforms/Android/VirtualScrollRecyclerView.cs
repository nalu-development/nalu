using Android.Content;
using Android.Runtime;
using Android.Util;
using AndroidX.Core.View;
using AndroidX.RecyclerView.Widget;
using AView = Android.Views.View;

namespace Nalu;

internal class VirtualScrollRecyclerView : RecyclerView, IOnApplyWindowInsetsListener
{
    private VirtualScrollRecyclerViewScrollHelper? _scrollHelper;
    public ItemsLayoutOrientation Orientation { get; set; } = ItemsLayoutOrientation.Vertical;
    public Action? OnLayoutCallback { get; set; }
    public VirtualScrollRecyclerViewScrollHelper ScrollHelper => _scrollHelper ??= new VirtualScrollRecyclerViewScrollHelper(this);

    protected VirtualScrollRecyclerView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public VirtualScrollRecyclerView(Context context) : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
        SetClipToPadding(false);
        SetClipChildren(true);
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
            ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        }

        base.Dispose(disposing);
    }

    private static readonly int _allInsetsType = WindowInsetsCompat.Type.SystemBars() |
                                                 WindowInsetsCompat.Type.DisplayCutout() |
                                                 WindowInsetsCompat.Type.NavigationBars() |
                                                 WindowInsetsCompat.Type.StatusBars() |
                                                 WindowInsetsCompat.Type.Ime();
    private static readonly AndroidX.Core.Graphics.Insets _zeroInsets = AndroidX.Core.Graphics.Insets.None!;
    
    WindowInsetsCompat AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);
        var size = insets.GetInsets(_allInsetsType) ?? _zeroInsets;
        if (PaddingBottom != size.Bottom || PaddingLeft != size.Left || PaddingRight != size.Right || PaddingTop != size.Top)
        {
            SetPadding(size.Left, size.Top, size.Right, size.Bottom);
            RequestLayout();
        }

        using var builder = new WindowInsetsCompat.Builder(insets);
        return builder.SetInsets(_allInsetsType, _zeroInsets)!.Build()!;
    }
}
