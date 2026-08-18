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
        private ScrollChangedListener? _scrollChangedListener;

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
                // An EXPLICIT ViewTreeObserver listener object (never the C# event sugar):
                // a pre-window-attach observer DIES on attach and silently migrates its
                // listeners to the window's live observer — removal must target the CURRENT
                // observer, which only works with a listener instance we own.
                _scrollChangedListener = new ScrollChangedListener(this);
                target.ViewTreeObserver!.AddOnScrollChangedListener(_scrollChangedListener);
                PushScrollY();
            }
        }

        partial void PlatformDetach()
        {
            if (_recyclerListener is not null)
            {
                (_target as RecyclerView)?.RemoveOnScrollListener(_recyclerListener);
                _recyclerListener.Disconnect();
                _recyclerListener = null;
            }

            if (_scrollChangedListener is not null)
            {
                try
                {
                    // The CURRENT observer (migrated listeners live there, not on the one we
                    // registered with). A dead observer throws IllegalStateException.
                    _target?.ViewTreeObserver?.RemoveOnScrollChangedListener(_scrollChangedListener);
                }
                catch (Java.Lang.IllegalStateException)
                {
                    // Observer already dead with no live successor: nothing to remove.
                }

                // Belt and braces: even a stranded Java-side registration must not retain the
                // subscription → tracked view → page chain.
                _scrollChangedListener.Disconnect();
                _scrollChangedListener = null;
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

        /// <summary>Breadth-first, shallowest scrollable wins, bounded by <see cref="ScaffoldScrollObserver._maxSearchDepth"/>.</summary>
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

                if (depth < _maxSearchDepth && view is AViewGroup group)
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

        private sealed class ScrollChangedListener(Subscription owner) : Java.Lang.Object, ViewTreeObserver.IOnScrollChangedListener
        {
            private Subscription? _owner = owner;

            public void Disconnect() => _owner = null;

            public void OnScrollChanged() => _owner?.PushScrollY();
        }

        private sealed class RecyclerScrollListener(Subscription owner) : RecyclerView.OnScrollListener
        {
            private Subscription? _owner = owner;

            public void Disconnect() => _owner = null;

            public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
                => _owner?.OnRecyclerScrolled(dy);
        }
    }
}
