using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Test hook simulating the long-press drag pipeline (visible to the TestApp only): drives
/// the SAME interactive-movement sequence the <c>UILongPressGestureRecognizer</c> handler
/// performs — delegate initiating, begin/update/end interactive movement, delegate ended —
/// so UI tests can exercise drag&amp;drop end-to-end on platforms where a real held
/// long-press cannot be injected from the test host.
/// </summary>
/// <remarks>
/// Everything downstream of the gesture is real: <c>CanMoveItem</c> (drag veto +
/// OnDragStarted), the data-source <c>MoveItem</c>, and the delegate lifecycle. Only Apple's
/// own long-press recognition is bypassed.
/// </remarks>
internal static class VirtualScrollDragSimulator
{
    public static async Task SimulateDragAsync(VirtualScroll virtualScroll, int fromIndex, int toIndex, int fromSection = 0, int toSection = 0)
    {
        if (virtualScroll.Handler?.PlatformView is not UIView root || FindCollectionView(root) is not { } collectionView)
        {
            throw new InvalidOperationException("VirtualScroll platform collection view not found.");
        }

        var fromPath = NSIndexPath.FromItemSection(fromIndex, fromSection);
        var toPath = NSIndexPath.FromItemSection(toIndex, toSection);

        // Mirrors HandleLongPress/Began: initiating precedes the CanMoveItem veto, which
        // UIKit evaluates inside BeginInteractiveMovementForItem.
        ((VirtualScrollDelegate) collectionView.Delegate).ItemDragInitiating(fromPath);
        collectionView.BeginInteractiveMovementForItem(fromPath);

        // Mirrors Changed: walk the touch point to the destination cell's center across a few
        // runloop turns — UIKit updates its movement preview (and target index) per update.
        var from = collectionView.GetLayoutAttributesForItem(fromPath)!.Frame.GetMidPoint();
        var to = collectionView.GetLayoutAttributesForItem(toPath)!.Frame.GetMidPoint();
        const int steps = 6;

        for (var step = 1; step <= steps; step++)
        {
            var x = from.X + ((to.X - from.X) * step / steps);
            var y = from.Y + ((to.Y - from.Y) * step / steps);
            collectionView.UpdateInteractiveMovement(new CoreGraphics.CGPoint(x, y));
            await Task.Delay(30);
        }

        // Mirrors Ended.
        collectionView.EndInteractiveMovement();
        ((VirtualScrollDelegate) collectionView.Delegate).ItemDragEnded();
    }

    private static UICollectionView? FindCollectionView(UIView view)
    {
        if (view is UICollectionView collectionView)
        {
            return collectionView;
        }

        foreach (var subview in view.Subviews)
        {
            if (FindCollectionView(subview) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static CoreGraphics.CGPoint GetMidPoint(this CoreGraphics.CGRect rect)
        => new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
}
