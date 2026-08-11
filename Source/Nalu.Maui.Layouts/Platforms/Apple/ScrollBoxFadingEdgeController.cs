using CoreAnimation;
using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Fading-edge mask for the ScrollBox scroll view, ported from VirtualScroll's
/// <c>FadingEdgeController</c>: a <c>CAGradientLayer</c> assigned to the scroll view's SUPERVIEW
/// layer mask, whose fade length on each side tracks the distance scrolled from that edge.
/// </summary>
internal sealed class ScrollBoxFadingEdgeController
{
    private enum FadeGradient
    {
        None,
        Left,
        Right,
        LeftRight,
        Top,
        Bottom,
        TopBottom
    }

    private FadeGradient _fadeGradient = FadeGradient.None;
    private CGRect _lastBounds = CGRect.Empty;
    private double _lastEndFadingEdgeLength;
    private double _lastStartFadingEdgeLength;

    public void Update(double fadingEdgeLength, ScrollBoxOrientation orientation, UIScrollView scrollView)
    {
        if (fadingEdgeLength <= 0)
        {
            UpdateFadeGradient(scrollView, fadingEdgeLength, fadingEdgeLength, FadeGradient.None);

            return;
        }

        if (orientation == ScrollBoxOrientation.Horizontal)
        {
            var distanceFromLeft = scrollView.ContentOffset.X + scrollView.AdjustedContentInset.Left;
            var distanceFromRight = scrollView.ContentSize.Width + scrollView.AdjustedContentInset.Right - scrollView.Bounds.Width - scrollView.ContentOffset.X;
            var isAtLeft = distanceFromLeft <= 0;
            var isAtRight = distanceFromRight <= 0;

            UpdateFadeGradient(
                scrollView,
                Math.Min(fadingEdgeLength, distanceFromLeft),
                Math.Min(fadingEdgeLength, distanceFromRight),
                isAtLeft && isAtRight ? FadeGradient.None : isAtLeft ? FadeGradient.Left : isAtRight ? FadeGradient.Right : FadeGradient.LeftRight
            );
        }
        else
        {
            var distanceFromTop = scrollView.ContentOffset.Y + scrollView.AdjustedContentInset.Top;
            var distanceFromBottom = scrollView.ContentSize.Height + scrollView.AdjustedContentInset.Bottom - scrollView.Bounds.Height - scrollView.ContentOffset.Y;
            var isAtTop = distanceFromTop <= 0;
            var isAtBottom = distanceFromBottom <= 0;

            UpdateFadeGradient(
                scrollView,
                Math.Min(fadingEdgeLength, distanceFromTop),
                Math.Min(fadingEdgeLength, distanceFromBottom),
                isAtTop && isAtBottom ? FadeGradient.None : isAtTop ? FadeGradient.Top : isAtBottom ? FadeGradient.Bottom : FadeGradient.TopBottom
            );
        }
    }

    private void UpdateFadeGradient(UIScrollView uiScrollView, double startFadingEdgeLength, double endFadingEdgeLength, FadeGradient fadeGradient)
    {
        if (uiScrollView.Superview is not { } superview)
        {
            return;
        }

        var bounds = superview.Bounds;

        if (bounds.IsEmpty)
        {
            superview.Layer.Mask = null;

            return;
        }

        if (_fadeGradient == fadeGradient &&
            _lastStartFadingEdgeLength == startFadingEdgeLength &&
            _lastEndFadingEdgeLength == endFadingEdgeLength &&
            (fadeGradient == FadeGradient.None || _lastBounds == bounds))
        {
            return;
        }

        _lastBounds = bounds;
        _fadeGradient = fadeGradient;
        _lastStartFadingEdgeLength = startFadingEdgeLength;
        _lastEndFadingEdgeLength = endFadingEdgeLength;

        if (fadeGradient == FadeGradient.None)
        {
            superview.Layer.Mask = null;
            superview.SetNeedsDisplay();

            return;
        }

        var gradientLayer = new CAGradientLayer
        {
            Frame = bounds
        };

        // Make gradient horizontal (default is vertical)
        var gradientWidth = bounds.Height;

        if (fadeGradient <= FadeGradient.LeftRight)
        {
            gradientWidth = bounds.Width;
            gradientLayer.StartPoint = new CGPoint(0.0, 0.5);
            gradientLayer.EndPoint = new CGPoint(1.0, 0.5);
        }

        var startFadeWidth = startFadingEdgeLength / Math.Max(1, gradientWidth);
        var endFadeWidth = endFadingEdgeLength / Math.Max(1, gradientWidth);

        switch (fadeGradient)
        {
            case FadeGradient.Right:
            case FadeGradient.Bottom:
                // At the end edge: only the start side fades (its length follows the distance scrolled from start).
                gradientLayer.Locations = [0, startFadeWidth];
                gradientLayer.Colors = [UIColor.Clear.CGColor, UIColor.Black.CGColor];

                break;
            case FadeGradient.Left:
            case FadeGradient.Top:
                // At the start edge: only the end side fades (its length follows the remaining scrollable distance).
                gradientLayer.Locations = [1 - endFadeWidth, 1];
                gradientLayer.Colors = [UIColor.Black.CGColor, UIColor.Clear.CGColor];

                break;
            case FadeGradient.LeftRight:
            case FadeGradient.TopBottom:
                gradientLayer.Locations = [0, startFadeWidth, 1 - endFadeWidth, 1];
                gradientLayer.Colors = [UIColor.Clear.CGColor, UIColor.Black.CGColor, UIColor.Black.CGColor, UIColor.Clear.CGColor];

                break;
        }

        superview.Layer.Mask = gradientLayer;
        superview.SetNeedsDisplay();
    }
}
