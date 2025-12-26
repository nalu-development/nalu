using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Platform;

namespace Nalu;

public class VirtualScrollViewWrapper : FrameLayout
{
    private WeakReference<IView>? _virtualView;

    public IView? VirtualView
    {
        get => _virtualView?.TryGetTarget(out var view) == true ? view : null;
        set => _virtualView = value is null ? null : new WeakReference<IView>(value);
    }

    protected VirtualScrollViewWrapper(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public VirtualScrollViewWrapper(Context context, IAttributeSet? attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
    {
    }

    public VirtualScrollViewWrapper(Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
    {
    }

    public VirtualScrollViewWrapper(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
    }

    public VirtualScrollViewWrapper(Context context) : base(context)
    {
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (LayoutParameters!.Width == ViewGroup.LayoutParams.MatchParent)
        {
            var width = MeasureSpec.GetSize(widthMeasureSpec);
            widthMeasureSpec = MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly);
        }
        else
        {
            var height = MeasureSpec.GetSize(heightMeasureSpec);
            heightMeasureSpec = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
        }

        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        var destination = Context!.ToCrossPlatformRectInReferenceFrame(left, top, right, bottom);
        VirtualView?.Arrange(destination);
    }
}
