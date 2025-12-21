using CoreAnimation;
using CoreGraphics;
using UIKit;

namespace Nalu;

internal class FadingEdgeController
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

    public void Update(double fadingEdgeLength, ItemsLayoutOrientation orientation, UIScrollView scrollView)
    {
        if (fadingEdgeLength <= 0)
        {
            UpdateFadeGradient(scrollView, fadingEdgeLength, fadingEdgeLength, FadeGradient.None);
            return;
        }

        switch (orientation)
        {
            case ItemsLayoutOrientation.Horizontal:
                var distanceFromLeft = scrollView.GetDistanceFromLeft();
                var distanceFromRight = scrollView.GetDistanceFromRight();
                var isAtLeft = distanceFromLeft <= 0;
                var isAtRight = distanceFromRight <= 0;

                UpdateFadeGradient(
                    scrollView,
                    Math.Min(fadingEdgeLength, distanceFromLeft),
                    Math.Min(fadingEdgeLength, distanceFromRight),
                    isAtLeft && isAtRight ? FadeGradient.None : isAtLeft ? FadeGradient.Left : isAtRight ? FadeGradient.Right : FadeGradient.LeftRight
                );

                break;
            case ItemsLayoutOrientation.Vertical:
                var distanceFromTop = scrollView.GetDistanceFromTop();
                var distanceFromBottom = scrollView.GetDistanceFromBottom();
                var isAtTop = distanceFromTop <= 0;
                var isAtBottom = distanceFromBottom <= 0;

                UpdateFadeGradient(
                    scrollView,
                    Math.Min(fadingEdgeLength, distanceFromTop),
                    Math.Min(fadingEdgeLength, distanceFromBottom),
                    isAtTop && isAtBottom ? FadeGradient.None : isAtTop ? FadeGradient.Top : isAtBottom ? FadeGradient.Bottom : FadeGradient.TopBottom
                );

                break;
            default:
                UpdateFadeGradient(scrollView, fadingEdgeLength, fadingEdgeLength, FadeGradient.None);
                break;
        }
    }

    private void UpdateFadeGradient(UIScrollView uiScrollView, double startFadingEdgeLength, double endFadingEdgeLength, FadeGradient fadeGradient)
    {
        var superview = uiScrollView.Superview!;
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
                gradientLayer.Locations = [0, endFadeWidth];
                gradientLayer.Colors = [UIColor.Clear.CGColor, UIColor.Black.CGColor];
                break;
            case FadeGradient.Left:
            case FadeGradient.Top:
                gradientLayer.Locations = [1 - startFadeWidth, 1];
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

