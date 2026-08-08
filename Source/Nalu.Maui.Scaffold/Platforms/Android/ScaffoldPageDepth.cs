using Android.Graphics;
using Paint = Android.Graphics.Paint;
using Color = Android.Graphics.Color;
using Android.Graphics.Drawables;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Depth cues for stacked page motion (push, pop, predictive back), drawn into the REVEALED
/// page's foreground: a dim proportional to how covered it still is, plus an edge-shadow
/// gradient anchored at the seam under the moving page's leading edge. Drawn by us on purpose —
/// platform elevation shadows are scaled by OEM theme alphas (verified near-invisible on
/// OxygenOS), while a foreground drawable renders identically everywhere, needs no hierarchy
/// changes, and stays below the overlay flights. The dim strength mirrors Android's own
/// predictive-back scrim (stronger than the iOS counterpart, which matches ITS platform look
/// and keeps the reliable layer shadow instead). Side-by-side motions (root switches) get
/// neither cue.
/// </summary>
internal static class ScaffoldPageDepth
{
    /// <summary>Max dim strength at full coverage (matches the system predictive-back scrim).</summary>
    private const float _maxDimAlpha = 0.30f;

    /// <summary>
    /// Peak alpha of the edge shadow at the seam. Deliberately soft: iOS renders a blurred
    /// gaussian layer shadow, and a strong linear band reads as a hard stripe next to it —
    /// a wider, weaker, eased gradient matches that look.
    /// </summary>
    private const float _edgeShadowAlpha = 0.16f;

    private const float _edgeShadowWidthDp = 40f;

    /// <summary>
    /// Keeps the moving page stacked ABOVE the revealed one (whatever elevation shadow the
    /// device theme still draws is a bonus). The guaranteed visible cue is the seam shadow
    /// drawn by <see cref="SetDepth"/> on the page below.
    /// </summary>
    public static void ApplyShadow(AView view) => view.TranslationZ = view.Context!.ToPixels(12);

    /// <summary>Clears the elevation applied by <see cref="ApplyShadow"/>.</summary>
    public static void ClearShadow(AView view) => view.TranslationZ = 0f;

    /// <summary>
    /// Draws the revealed page's depth state: <paramref name="coverage"/> is how covered it
    /// still is (1 = fully covered, 0 = fully revealed — removes everything);
    /// <paramref name="seamPx"/> is the moving page's leading edge in the revealed page's
    /// coordinates — the edge shadow hugs it from the left. Pass 0 to omit the seam shadow.
    /// </summary>
    public static void SetDepth(AView view, float coverage, float seamPx)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return;
        }

        coverage = Math.Clamp(coverage, 0f, 1f);

        if (coverage <= 0f)
        {
            if (view.Foreground is DepthDrawable)
            {
                view.Foreground = null;
            }

            return;
        }

        if (view.Foreground is not DepthDrawable depth)
        {
            depth = new DepthDrawable(view.Context!.ToPixels(_edgeShadowWidthDp));
            view.Foreground = depth;
        }

        depth.Update(
            dimAlpha: (int) (coverage * _maxDimAlpha * 255),
            shadowAlpha: (int) (_edgeShadowAlpha * 255),
            seamPx
        );
    }

    /// <summary>The dim + seam-shadow pair, identifiable so foreign foregrounds are never clobbered.</summary>
    private sealed class DepthDrawable(float shadowWidthPx) : Drawable
    {
        private readonly Paint _dimPaint = new();
        private readonly Paint _shadowPaint = new();
        private readonly Matrix _shadowMatrix = new();
        private LinearGradient? _shadowShader;
        private int _dimAlpha;
        private int _shadowAlpha;
        private float _seamPx;

        public void Update(int dimAlpha, int shadowAlpha, float seamPx)
        {
            _dimAlpha = dimAlpha;
            _shadowAlpha = shadowAlpha;
            _seamPx = seamPx;
            InvalidateSelf();
        }

        public override void Draw(Canvas canvas)
        {
            var bounds = Bounds;

            if (_dimAlpha > 0)
            {
                _dimPaint.Color = Color.Argb(_dimAlpha, 0, 0, 0);
                canvas.DrawRect(bounds, _dimPaint);
            }

            if (_shadowAlpha > 0 && _seamPx > 0)
            {
                // One gradient, translated to the seam per frame: no per-frame allocations.
                // Eased three-stop ramp (quadratic-ish): reads like a blurred shadow, not a band.
                _shadowShader ??= new LinearGradient(
                    0,
                    0,
                    shadowWidthPx,
                    0,
                    [Color.Argb(0, 0, 0, 0).ToArgb(), Color.Argb(64, 0, 0, 0).ToArgb(), Color.Argb(255, 0, 0, 0).ToArgb()],
                    [0f, 0.55f, 1f],
                    Shader.TileMode.Clamp!
                );

                _shadowPaint.SetShader(_shadowShader);

                var seam = Math.Min(_seamPx, bounds.Right);
                _shadowMatrix.SetTranslate(seam - shadowWidthPx, 0);
                _shadowShader.SetLocalMatrix(_shadowMatrix);
                _shadowPaint.Alpha = _shadowAlpha;
                canvas.DrawRect(seam - shadowWidthPx, bounds.Top, seam, bounds.Bottom, _shadowPaint);
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
