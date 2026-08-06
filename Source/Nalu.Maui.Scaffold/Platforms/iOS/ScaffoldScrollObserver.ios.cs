using Foundation;
using UIKit;

namespace Nalu.Internals;

internal static partial class ScaffoldScrollObserver
{
    private sealed partial class Subscription
    {
        private IDisposable? _kvo;

        partial void PlatformAttach()
        {
            if (_view.Handler?.PlatformView is not UIView root || FindScrollView(root) is not { } scrollView)
            {
                return;
            }

            // KVO on contentOffset covers every UIScrollView subclass (collection, table and
            // web views included) and fires per frame, momentum and bounce included.
            // 0 = rest position: the adjusted top inset is folded back in.
            _kvo = scrollView.AddObserver(
                "contentOffset",
                NSKeyValueObservingOptions.New | NSKeyValueObservingOptions.Initial,
                _ => _onOffsetDp(scrollView.ContentOffset.Y + scrollView.AdjustedContentInset.Top)
            );
        }

        partial void PlatformDetach()
        {
            _kvo?.Dispose();
            _kvo = null;
        }

        /// <summary>Breadth-first, shallowest scrollable wins, bounded by <see cref="MaxSearchDepth"/>.</summary>
        private static UIScrollView? FindScrollView(UIView root)
        {
            var queue = new Queue<(UIView View, int Depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (view, depth) = queue.Dequeue();

                if (view is UIScrollView scrollView)
                {
                    return scrollView;
                }

                if (depth < MaxSearchDepth)
                {
                    foreach (var subview in view.Subviews)
                    {
                        queue.Enqueue((subview, depth + 1));
                    }
                }
            }

            return null;
        }
    }
}
