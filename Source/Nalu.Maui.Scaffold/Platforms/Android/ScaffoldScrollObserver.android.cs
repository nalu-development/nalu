using Android.Views;
using AndroidX.RecyclerView.Widget;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace Nalu.Internals;

internal static partial class ScaffoldScrollObserver
{
    private sealed partial class Subscription
    {
        private AView? _target;
        private float _density = 1;
        private double _recyclerOffsetPx;
        private RecyclerScrollListener? _recyclerListener;
        private ViewTreeObserver? _treeObserver;
        private EventHandler? _treeScrollHandler;

        partial void PlatformAttach()
        {
            if (_view.Handler?.PlatformView is not AView root || FindScrollable(root) is not { } target)
            {
                return;
            }

            _target = target;
            _density = target.Resources?.DisplayMetrics?.Density ?? 1;

            if (target is RecyclerView recyclerView)
            {
                // No absolute offset exists on RecyclerView: accumulate deltas from attach
                // (additive listener — safe next to the component's own listeners).
                _recyclerOffsetPx = 0;
                _recyclerListener = new RecyclerScrollListener(this);
                recyclerView.AddOnScrollListener(_recyclerListener);
                _onOffsetDp(0);
            }
            else
            {
                // ViewTreeObserver.ScrollChanged is additive (unlike the single-slot
                // View.SetOnScrollChangeListener, which MAUI may already occupy) and works on
                // the API 21 floor; reading ScrollY per callback is cheap.
                _treeScrollHandler = (_, _) => PushScrollY();
                _treeObserver = target.ViewTreeObserver;
                _treeObserver!.ScrollChanged += _treeScrollHandler;
                PushScrollY();
            }
        }

        partial void PlatformDetach()
        {
            if (_recyclerListener is not null)
            {
                (_target as RecyclerView)?.RemoveOnScrollListener(_recyclerListener);
                _recyclerListener = null;
            }

            if (_treeScrollHandler is not null)
            {
                if (_treeObserver is { IsAlive: true })
                {
                    _treeObserver.ScrollChanged -= _treeScrollHandler;
                }

                _treeObserver = null;
                _treeScrollHandler = null;
            }

            _target = null;
        }

        private void PushScrollY()
        {
            if (_target is { } target)
            {
                _onOffsetDp(target.ScrollY / _density);
            }
        }

        private void OnRecyclerScrolled(int dyPx)
        {
            _recyclerOffsetPx += dyPx;
            _onOffsetDp(_recyclerOffsetPx / _density);
        }

        /// <summary>Breadth-first, shallowest scrollable wins, bounded by <see cref="MaxSearchDepth"/>.</summary>
        private static AView? FindScrollable(AView root)
        {
            var queue = new Queue<(AView View, int Depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (view, depth) = queue.Dequeue();

                if (view is RecyclerView
                    or Android.Widget.ScrollView
                    or AndroidX.Core.Widget.NestedScrollView
                    or Android.Widget.AbsListView
                    or Android.Webkit.WebView)
                {
                    return view;
                }

                if (depth < MaxSearchDepth && view is AViewGroup group)
                {
                    for (var i = 0; i < group.ChildCount; i++)
                    {
                        if (group.GetChildAt(i) is { } child)
                        {
                            queue.Enqueue((child, depth + 1));
                        }
                    }
                }
            }

            return null;
        }

        private sealed class RecyclerScrollListener(Subscription owner) : RecyclerView.OnScrollListener
        {
            public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
                => owner.OnRecyclerScrolled(dy);
        }
    }
}
