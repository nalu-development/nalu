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
        // The cell size must come from the MAUI cross-platform measure: the native
        // FrameLayout measure of the platform child ignores cross-platform layout
        // (margins, Width/HeightRequest, MAUI layout logic), producing clipped cells.
        if (VirtualView is not { } virtualView || Context is not { } context)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);

            return;
        }

        var widthMode = MeasureSpec.GetMode(widthMeasureSpec);
        var heightMode = MeasureSpec.GetMode(heightMeasureSpec);
        var pixelWidth = MeasureSpec.GetSize(widthMeasureSpec);
        var pixelHeight = MeasureSpec.GetSize(heightMeasureSpec);

        var widthConstraint = widthMode == MeasureSpecMode.Unspecified ? double.PositiveInfinity : context.FromPixels(pixelWidth);
        var heightConstraint = heightMode == MeasureSpecMode.Unspecified ? double.PositiveInfinity : context.FromPixels(pixelHeight);

        var measured = virtualView.Measure(widthConstraint, heightConstraint);

        // Ceil fractional device-independent sizes so content is never clipped by a sub-pixel.
        var measuredWidth = widthMode == MeasureSpecMode.Exactly ? pixelWidth : (int) Math.Ceiling(context.ToPixels(measured.Width));
        var measuredHeight = heightMode == MeasureSpecMode.Exactly ? pixelHeight : (int) Math.Ceiling(context.ToPixels(measured.Height));

        SetMeasuredDimension(measuredWidth, measuredHeight);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        var destination = Context!.ToCrossPlatformRectInReferenceFrame(left, top, right, bottom);
        VirtualView?.Arrange(destination);
    }
}
