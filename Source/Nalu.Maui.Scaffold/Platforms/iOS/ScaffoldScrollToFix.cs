using CoreGraphics;
using Microsoft.Maui.Handlers;
using UIKit;

namespace Nalu;

/// <summary>
/// Workaround for a .NET MAUI iOS defect (present in 10.0.80–10.0.90):
/// <c>ScrollViewHandler.MapRequestScrollTo</c> clamps the target offset to
/// <c>ContentSize - Frame</c> WITHOUT the native <c>AdjustedContentInset</c>, so programmatic
/// scrolls on any inset-consuming scroll view (the standard .NET 10 edge-to-edge setup — and
/// every scaffold-hosted page, whose chrome augments the safe area) stop short of the true end
/// of content by exactly the inset sum.
/// This hook re-clamps against the inset-aware maximum and leaves everything else — including
/// MAUI's content-coordinate convention — untouched. Remove once fixed upstream.
/// </summary>
/// <remarks>
/// To be removed when https://github.com/dotnet/maui/issues/3680 gets fixed
/// </remarks>
internal static class ScaffoldScrollToFix
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied)
        {
            return;
        }

        _applied = true;

        ScrollViewHandler.CommandMapper.ModifyMapping(
            nameof(IScrollView.RequestScrollTo),
            static (handler, scrollView, args, baseMethod) =>
            {
                if (args is ScrollToRequest request
                    && handler.PlatformView is UIScrollView native
                    && native.ContentSize != CGSize.Empty
                    && native.AdjustedContentInset != UIEdgeInsets.Zero)
                {
                    var adjusted = native.AdjustedContentInset;
                    var maxX = (double)(native.ContentSize.Width + adjusted.Right - native.Bounds.Width);
                    var maxY = (double)(native.ContentSize.Height + adjusted.Bottom - native.Bounds.Height);

                    var targetX = Math.Min(request.HorizontalOffset, maxX);
                    var targetY = Math.Min(request.VerticalOffset, maxY);

                    native.SetContentOffset(new CGPoint(targetX, targetY), !request.Instant);

                    if (request.Instant)
                    {
                        scrollView.ScrollFinished();
                    }

                    return;
                }

                // Zero insets (or pending-layout state): the stock mapping is correct there.
                baseMethod?.Invoke(handler, scrollView, args);
            }
        );
    }
}
