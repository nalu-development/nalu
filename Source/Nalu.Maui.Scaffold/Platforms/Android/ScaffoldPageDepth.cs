using Android.Graphics.Drawables;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Depth cues for stacked page motion (push, pop, predictive back): an elevation shadow on the
/// page moving ABOVE (its boundary reads against any content) and a subtle dim on the page
/// revealed BENEATH, proportional to how covered it still is. Side-by-side motions (root
/// switches) get neither — the pages are adjacent, not stacked.
/// </summary>
internal static class ScaffoldPageDepth
{
    private const float _shadowElevationDp = 12f;

    /// <summary>Max dim strength at full coverage (matches the platform nav containers' subtlety).</summary>
    private const float _maxDimAlpha = 0.15f;

    /// <summary>The shadow elevation in pixels for the given view's context.</summary>
    public static float ShadowPx(AView view) => view.Context!.ToPixels(_shadowElevationDp);

    /// <summary>
    /// Raises the view and lets the framework draw its elevation shadow (RenderThread-composited:
    /// the shadow travels with the view's translation at no per-frame cost). Requires the view's
    /// outline — pages with a background have one; a background-less page simply casts nothing.
    /// </summary>
    public static void ApplyShadow(AView view) => view.TranslationZ = ShadowPx(view);

    /// <summary>Clears the elevation applied by <see cref="ApplyShadow"/>.</summary>
    public static void ClearShadow(AView view) => view.TranslationZ = 0f;

    /// <summary>
    /// Dims the revealed page for how covered it still is (1 = fully covered, 0 = fully
    /// revealed — removes the dim). Drawn via the view's foreground so no hierarchy changes are
    /// needed wherever the page happens to be hosted; overlay flights (container overlay) stay
    /// above and undimmed.
    /// </summary>
    public static void SetDim(AView view, float coverage)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return;
        }

        coverage = Math.Clamp(coverage, 0f, 1f);

        if (coverage <= 0f)
        {
            if (view.Foreground is ColorDrawable)
            {
                view.Foreground = null;
            }

            return;
        }

        var alpha = (int) (coverage * _maxDimAlpha * 255);

        if (view.Foreground is ColorDrawable dim)
        {
            dim.Alpha = alpha;
        }
        else
        {
            view.Foreground = new ColorDrawable(Android.Graphics.Color.Black) { Alpha = alpha };
        }
    }
}
