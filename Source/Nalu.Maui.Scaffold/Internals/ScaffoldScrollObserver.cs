namespace Nalu.Internals;

/// <summary>
/// Native scroll observation behind <see cref="Scaffold.ScrollTrackerProperty"/>: turns the
/// tracked view's actual scroll position into per-frame dp callbacks feeding
/// <see cref="ScaffoldNavBarContext.ScrollOffset"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tracked MAUI view is often NOT the scrollable itself: component roots wrap their native
/// scrollable in container views (VirtualScroll being the canonical case). The observer
/// breadth-first searches the tracked view's PLATFORM subtree up to
/// <see cref="_maxSearchDepth"/> levels for the first scrollable platform view
/// (iOS: any <c>UIScrollView</c> — covering collection/table/web views too;
/// Android: scroll containers and <c>RecyclerView</c>).
/// </para>
/// <para>
/// Android <c>RecyclerView</c> has no absolute offset — its value is ACCUMULATED from scroll
/// deltas starting at attach (drift with variable item heights is inherent; thresholds are
/// reliable, exact pixel mapping over long distances is not).
/// </para>
/// </remarks>
internal static partial class ScaffoldScrollObserver
{
    /// <summary>How deep below the tracked view's platform root the scrollable may live.</summary>
    internal const int _maxSearchDepth = 3;

    /// <summary>
    /// Observes the given tracked view, invoking <paramref name="onOffsetDp"/> with the
    /// vertical offset in dp (0 = rest position; negative while over-scrolling at the top).
    /// Attachment is handler-aware (waits for / survives handler churn). Dispose to stop.
    /// </summary>
    public static IDisposable Observe(View trackedView, Action<double> onOffsetDp)
        => new Subscription(trackedView, onOffsetDp);

    private sealed partial class Subscription : IDisposable
    {
        private readonly View _view;
        private readonly Action<double> _onOffsetDp;
        private bool _disposed;

        public Subscription(View view, Action<double> onOffsetDp)
        {
            _view = view;
            _onOffsetDp = onOffsetDp;
            view.HandlerChanged += OnHandlerChanged;
            TryAttach();
        }

        private void OnHandlerChanged(object? sender, EventArgs e) => TryAttach();

        private void TryAttach()
        {
            if (!_disposed && _view.Handler?.PlatformView is not null)
            {
                PlatformDetach();
                PlatformAttach();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.HandlerChanged -= OnHandlerChanged;
            PlatformDetach();
        }

        partial void PlatformAttach();

        partial void PlatformDetach();
    }
}
