using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Platform;

namespace Nalu;

/// <summary>
/// How a cell's content uses the space the cell was given.
/// </summary>
internal enum VirtualScrollCellContentExtent
{
    /// <summary>The content fills the cell — what a list or a carousel wants.</summary>
    Fill,

    /// <summary>The content keeps its measured height and sits at the top of the cell.</summary>
    NaturalHeight,

    /// <summary>The content keeps its measured width and sits at the leading edge of the cell.</summary>
    NaturalWidth
}

public class VirtualScrollViewWrapper : FrameLayout
{
    private WeakReference<IView>? _virtualView;
    private bool _hasMeasured;
    private double _lastWidthConstraint;
    private double _lastHeightConstraint;
    private Microsoft.Maui.Graphics.Size _lastMeasuredSize;

    public IView? VirtualView
    {
        get => _virtualView?.TryGetTarget(out var view) == true ? view : null;
        set
        {
            _virtualView = value is null ? null : new WeakReference<IView>(value);
            InvalidateMeasureCache();
        }
    }

    /// <summary>
    /// Whether the content should fill the cell or keep its own extent along the scrolling axis.
    /// </summary>
    /// <remarks>
    /// A grid line is as long as its longest cell, and <c>GridLayoutManager</c> stretches
    /// every cell in the line to that extent — it re-measures the shorter ones with an exact spec,
    /// which a cell cannot decline. Leaving the extra space to the cell rather than to its content
    /// is what keeps a grid line looking the same here as it does on UIKit, where a self-sizing
    /// item keeps its own extent.
    /// </remarks>
    internal VirtualScrollCellContentExtent ContentExtent { get; set; } = VirtualScrollCellContentExtent.Fill;

    /// <summary>
    /// Forgets the last measure, so the next one goes through the cross-platform layout again.
    /// </summary>
    /// <remarks>
    /// Must be called whenever the cell's content changes: the wrapper keeps the same
    /// <see cref="VirtualView" /> across recycling, and only the binding context changes, so
    /// nothing else would tell the short-circuit in <see cref="OnMeasure" /> that the size it
    /// remembers belongs to different content.
    /// </remarks>
    public void InvalidateMeasureCache() => _hasMeasured = false;

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

        // Both dimensions are dictated by the caller, so the cross-platform measure result would be
        // discarded below. This is the grid's line-equalizing pass: GridLayoutManager measures every
        // cell in a line, then re-measures the shorter ones at the line extent, holding the cross
        // axis fixed. Re-entering the MAUI measure for a size we already know is pure waste, and it
        // would also overwrite the natural size OnLayout arranges the content at.
        // Two guards keep this to that pass alone: _hasMeasured is dropped once the cell is laid
        // out, so a later traversal always measures again, and one axis must be unchanged, so a
        // genuinely new size never takes this path.
        if (_hasMeasured
            && widthMode == MeasureSpecMode.Exactly
            && heightMode == MeasureSpecMode.Exactly
            && (widthConstraint == _lastWidthConstraint || heightConstraint == _lastHeightConstraint))
        {
            SetMeasuredDimension(pixelWidth, pixelHeight);

            return;
        }

        var measured = virtualView.Measure(widthConstraint, heightConstraint);

        _hasMeasured = true;
        _lastWidthConstraint = widthConstraint;
        _lastHeightConstraint = heightConstraint;
        _lastMeasuredSize = measured;

        // Ceil fractional device-independent sizes so content is never clipped by a sub-pixel.
        var measuredWidth = widthMode == MeasureSpecMode.Exactly ? pixelWidth : (int) Math.Ceiling(context.ToPixels(measured.Width));
        var measuredHeight = heightMode == MeasureSpecMode.Exactly ? pixelHeight : (int) Math.Ceiling(context.ToPixels(measured.Height));

        SetMeasuredDimension(measuredWidth, measuredHeight);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        var destination = Context!.ToCrossPlatformRectInReferenceFrame(left, top, right, bottom);

        // Hand the content only what it measured, so the slack a stretched grid line adds stays
        // with the cell. Never grows the rect: the measured size is what fitted the constraint.
        destination = ContentExtent switch
        {
            VirtualScrollCellContentExtent.NaturalHeight =>
                new Rect(destination.X, destination.Y, destination.Width, Math.Min(destination.Height, _lastMeasuredSize.Height)),
            VirtualScrollCellContentExtent.NaturalWidth =>
                new Rect(destination.X, destination.Y, Math.Min(destination.Width, _lastMeasuredSize.Width), destination.Height),
            _ => destination
        };

        VirtualView?.Arrange(destination);

        // The measure pass this cell belongs to is over. Anything measured from here on is a new
        // traversal — a resized container, say — and must go through the cross-platform measure
        // even when only one axis moved.
        InvalidateMeasureCache();
    }
}
