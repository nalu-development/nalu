using AndroidX.Core.View;
using AView = Android.Views.View;
using AInsets = AndroidX.Core.Graphics.Insets;

namespace Nalu;

/// <summary>
/// Positional safe-area self-padding for a ScrollBox scroller, ported from the VirtualScroll
/// Java layer (<c>VirtualScrollNativeRecyclerView</c>).
/// </summary>
/// <remarks>
/// <para>
/// Self-padding emulates UIKit's POSITIONAL safe-area model: each inset band is applied only
/// where it intersects the scroller's REST footprint — the layout position with every ancestor
/// scroll offset ignored. Rest coordinates keep the padding STABLE while an ancestor scroll view
/// scrolls this scroller under a system bar, while a full-screen scroller keeps its edge-to-edge
/// padding, and a strip resting mid-page gets none.
/// </para>
/// <para>
/// Unlike VirtualScroll this lives in managed code: the Java layer existed for per-cell JNI hot
/// paths, while a single-content scroller receives these callbacks a handful of times per layout,
/// not per recycled cell.
/// </para>
/// </remarks>
internal sealed class ScrollBoxInsetsController(AView owner)
{
    private static readonly int _allInsetsType = WindowInsetsCompat.Type.SystemBars()
                                                 | WindowInsetsCompat.Type.DisplayCutout()
                                                 | WindowInsetsCompat.Type.NavigationBars()
                                                 | WindowInsetsCompat.Type.StatusBars()
                                                 | WindowInsetsCompat.Type.Ime();

    private AInsets? _lastInsets;

    // Rest-intersection scratch (main-thread confined, valid until the next compute).
    private int _restLeft;
    private int _restTop;
    private int _restRight;
    private int _restBottom;

    /// <summary>
    /// Handles the scroller's <c>dispatchApplyWindowInsets</c>: caches the insets and applies the
    /// self-padding. The caller returns the insets UNCONSUMED and never traverses into the
    /// content, so MAUI's per-layout insets listeners inside the content are never invoked.
    /// </summary>
    public void OnDispatchApplyWindowInsets(Android.Views.WindowInsets insets)
    {
        if (WindowInsetsCompat.ToWindowInsetsCompat(insets, owner) is not { } compat)
        {
            return;
        }

        _lastInsets = compat.GetInsets(_allInsetsType);
        ApplySelfPadding();
    }

    /// <summary>
    /// Re-evaluates the rest-position self-padding now that geometry is known (the initial insets
    /// dispatch may pre-date layout); posted — padding cannot mutate mid-pass.
    /// </summary>
    public void OnLayout()
    {
        if (_lastInsets is { } insets)
        {
            ComputeRestIntersection(insets);

            if (SelfPaddingDiffers())
            {
                owner.Post(ApplySelfPadding);
            }
        }
    }

    private void ApplySelfPadding()
    {
        if (_lastInsets is not { } insets)
        {
            return;
        }

        // Recomputed here (not reused from OnLayout): geometry may have changed between the post
        // and this run, and the insets dispatch calls in directly.
        ComputeRestIntersection(insets);

        if (SelfPaddingDiffers())
        {
            owner.SetPadding(_restLeft, _restTop, _restRight, _restBottom);
            owner.RequestLayout();
        }
    }

    private bool SelfPaddingDiffers()
        => owner.PaddingBottom != _restBottom
           || owner.PaddingLeft != _restLeft
           || owner.PaddingRight != _restRight
           || owner.PaddingTop != _restTop;

    /// <summary>Computes the rest-footprint/inset intersection into the scratch fields.</summary>
    private void ComputeRestIntersection(AInsets size)
    {
        // Rest position: accumulate LAYOUT offsets up the chain, deliberately ignoring every
        // ancestor's scrollX/scrollY (scroll containers keep children's layout coordinates).
        var left = owner.Left;
        var top = owner.Top;
        var root = owner;

        for (var parent = owner.Parent; parent is AView parentView; parent = parentView.Parent)
        {
            // Inside a RECYCLING container the layout position itself is arbitrary (items are
            // re-laid-out as they scroll): never self-pad there.
            if (parentView is AndroidX.RecyclerView.Widget.RecyclerView or Android.Widget.AbsListView)
            {
                _restLeft = 0;
                _restTop = 0;
                _restRight = 0;
                _restBottom = 0;

                return;
            }

            left += parentView.Left;
            top += parentView.Top;
            root = parentView;
        }

        if (root.Width <= 0 || root.Height <= 0 || owner.Width <= 0 || owner.Height <= 0)
        {
            // Pre-layout dispatch: geometry unknown — keep the historical full padding;
            // OnLayout re-applies with the real rest position right after.
            _restLeft = size.Left;
            _restTop = size.Top;
            _restRight = size.Right;
            _restBottom = size.Bottom;

            return;
        }

        var right = left + owner.Width;
        var bottom = top + owner.Height;

        _restLeft = Math.Max(0, size.Left - left);
        _restTop = Math.Max(0, size.Top - top);
        _restRight = Math.Max(0, right - (root.Width - size.Right));
        _restBottom = Math.Max(0, bottom - (root.Height - size.Bottom));
    }
}
