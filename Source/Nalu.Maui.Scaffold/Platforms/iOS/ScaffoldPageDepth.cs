using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Depth cues for stacked page motion (push, pop, interactive edge pop): a layer shadow on the
/// page moving ABOVE (its boundary reads against any content) and a subtle dim on the page
/// revealed BENEATH, proportional to how covered it still is. Side-by-side motions (root
/// switches) get neither — the pages are adjacent, not stacked.
/// </summary>
internal static class ScaffoldPageDepth
{
    private const float _maxDimAlpha = 0.15f;
    private const int _dimViewTag = 0x4E414C55; // "NALU"

    /// <summary>
    /// Puts a shadow on the moving page. The explicit <c>ShadowPath</c> is what keeps it cheap:
    /// with it Core Animation pre-renders the shadow and moving the layer costs nothing;
    /// without it every frame pays an offscreen pass.
    /// </summary>
    public static void ApplyShadow(UIView view)
    {
        var layer = view.Layer;
        layer.ShadowColor = UIColor.Black.CGColor;
        layer.ShadowOpacity = 0.25f;
        layer.ShadowRadius = 12f;
        layer.ShadowOffset = CGSize.Empty;
        layer.ShadowPath = UIBezierPath.FromRect(view.Bounds).CGPath;
    }

    /// <summary>Clears the shadow applied by <see cref="ApplyShadow"/>.</summary>
    public static void ClearShadow(UIView view)
    {
        view.Layer.ShadowOpacity = 0f;
        view.Layer.ShadowPath = null;
    }

    /// <summary>
    /// Dims the revealed page for how covered it still is (1 = fully covered, 0 = fully
    /// revealed — removes the overlay). The overlay is a subview of the page, so it rides its
    /// frame and leaves with it; shared-element flights (container overlay) draw above it.
    /// </summary>
    public static void SetDim(UIView view, float coverage)
    {
        coverage = Math.Clamp(coverage, 0f, 1f);

        if (coverage <= 0f)
        {
            RemoveDim(view);

            return;
        }

        EnsureDim(view).Alpha = coverage * _maxDimAlpha;
    }

    /// <summary>Animates the dim toward the given coverage alongside a page animation.</summary>
    public static Task AnimateDimAsync(UIView view, float coverage, double durationSeconds)
    {
        var dim = EnsureDim(view);

        return UIView.AnimateAsync(durationSeconds, () => dim.Alpha = Math.Clamp(coverage, 0f, 1f) * _maxDimAlpha);
    }

    /// <summary>Removes the dim overlay entirely.</summary>
    public static void RemoveDim(UIView view) => view.ViewWithTag(_dimViewTag)?.RemoveFromSuperview();

    private static UIView EnsureDim(UIView view)
    {
        if (view.ViewWithTag(_dimViewTag) is { } existing)
        {
            return existing;
        }

        var dim = new UIView(view.Bounds)
        {
            Tag = _dimViewTag,
            BackgroundColor = UIColor.Black,
            UserInteractionEnabled = false,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
            Alpha = 0f
        };

        view.AddSubview(dim);

        return dim;
    }
}
