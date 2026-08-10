using Android.Graphics;
using Paint = Android.Graphics.Paint;
using Color = Android.Graphics.Color;
using Android.Graphics.Drawables;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Depth cues for stacked page motion (push, pop, predictive back): a dim drawn into the
/// REVEALED page's foreground, proportional to how covered it still is. Drawn by us on purpose —
/// a foreground drawable renders identically everywhere, needs no hierarchy changes, and stays
/// below the overlay flights. The strength mirrors Android's own predictive-back scrim.
/// The dim is the ONLY visible cue: an edge-shadow gradient anchored to the moving page was
/// tried and dropped — its invalidation lags a frame behind the animator-driven translation,
/// which reads as a shadow detached from the page during fast settles — and the elevation
/// shadow is suppressed too (see <see cref="ApplyShadow"/>), so both platforms present the
/// same dim-only look. Side-by-side motions (root switches) get no cue.
/// </summary>
internal static class ScaffoldPageDepth
{
    /// <summary>Max dim strength at full coverage (matches the system predictive-back scrim).</summary>
    private const float _maxDimAlpha = 0.30f;

    /// <summary>
    /// Keeps the moving page stacked ABOVE the revealed one. The null outline provider is what
    /// makes this a pure z-ORDER: without an outline the renderer draws no elevation shadow, so
    /// the dim stays the only visible cue on every device theme.
    /// </summary>
    public static void ApplyShadow(AView view)
    {
        view.OutlineProvider = null;
        view.TranslationZ = view.Context!.ToPixels(12);
    }

    /// <summary>Clears the stacking applied by <see cref="ApplyShadow"/>.</summary>
    public static void ClearShadow(AView view)
    {
        view.TranslationZ = 0f;
        view.OutlineProvider = Android.Views.ViewOutlineProvider.Background;
    }

    /// <summary>
    /// Draws the revealed page's dim: <paramref name="coverage"/> is how covered it still is
    /// (1 = fully covered, 0 = fully revealed — removes the overlay).
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
            if (view.Foreground is DimDrawable)
            {
                view.Foreground = null;
            }

            return;
        }

        if (view.Foreground is not DimDrawable dim)
        {
            dim = new DimDrawable();
            view.Foreground = dim;
        }

        dim.Update((int) (coverage * _maxDimAlpha * 255));
    }

    /// <summary>The dim, identifiable so foreign foregrounds are never clobbered.</summary>
    private sealed class DimDrawable : Drawable
    {
        private readonly Paint _dimPaint = new();
        private int _dimAlpha;

        public void Update(int dimAlpha)
        {
            _dimAlpha = dimAlpha;
            InvalidateSelf();
        }

        public override void Draw(Canvas canvas)
        {
            if (_dimAlpha > 0)
            {
                _dimPaint.Color = Color.Argb(_dimAlpha, 0, 0, 0);
                canvas.DrawRect(Bounds, _dimPaint);
            }
        }

        public override void SetAlpha(int alpha)
        {
        }

        public override void SetColorFilter(ColorFilter? colorFilter)
        {
        }

        public override int Opacity => (int) Format.Translucent;
    }
}
