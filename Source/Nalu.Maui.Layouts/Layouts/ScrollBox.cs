using System.Windows.Input;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// A scroll container designed as a dependable replacement for <see cref="ScrollView" />.
/// </summary>
/// <remarks>
/// <para>
/// ScrollBox hosts a single <see cref="Content" /> view and scrolls it along one axis
/// (<see cref="Orientation" />). It is built on the platform techniques proven in
/// Nalu.Maui.VirtualScroll: window insets are applied exactly once by the scroller itself
/// (positional self-padding on Android, <c>adjustedContentInset</c> on iOS), content sizing is
/// deterministic and convergent (<see cref="SizingStrategy" />), and
/// <see cref="ScrollToAsync(double, double, bool)" /> has a written completion contract — it
/// queues before the first layout, always completes its task, and clamps against adjusted insets.
/// </para>
/// <para>
/// <see cref="ScrollX" /> / <see cref="ScrollY" /> are distances scrolled from the start of the
/// content in device-independent units, consistent across platforms. They are refreshed on every
/// scroll frame while any scroll event subscriber exists, and always at the end of a scroll
/// gesture or programmatic scroll otherwise.
/// </para>
/// <para>
/// Known v1 limitations: no <c>Both</c> orientation; on Windows pull-to-refresh and
/// <see cref="FadingEdgeLength" /> are accepted but inactive, and the hugging
/// <see cref="SizingStrategy" /> modes size correctly at first layout but do not yet re-measure
/// on later content changes; with <c>FlowDirection=RightToLeft</c> horizontal offsets remain
/// left-based (descendant-targeting <see cref="ScrollToAsync(IView, ScrollToPosition, bool)" />
/// is position-correct regardless).
/// </para>
/// </remarks>
/// <example>
///     <code>
/// <![CDATA[
///     <nalu:ScrollBox SizingStrategy="480" FadingEdgeLength="24"
///                     IsRefreshEnabled="True" RefreshCommand="{Binding ReloadCommand}">
///         <VerticalStackLayout Padding="16" Spacing="8">
///             ...
///         </VerticalStackLayout>
///     </nalu:ScrollBox>
/// ]]>
/// </code>
/// </example>
[ContentProperty(nameof(Content))]
public class ScrollBox : ViewBoxBase, IScrollBox, IScrollBoxController
{
    private double _lastWidth;
    private double _lastHeight;

    #region Content

    /// <summary>
    /// Bindable property for <see cref="Content" /> property.
    /// </summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(IView),
        typeof(ScrollBox),
        propertyChanged: (bindable, oldValue, newValue) => ((ScrollBox) bindable).OnContentPropertyChanged((IView?) oldValue, (IView?) newValue)
    );

    /// <summary>
    /// Gets or sets the scrollable content.
    /// </summary>
    public IView? Content
    {
        get => GetContent();
        set => SetContent(value);
    }

    /// <inheritdoc />
    protected override IView? GetContent() => (IView?) GetValue(ContentProperty);

    /// <inheritdoc />
    protected override void SetContent(IView? content) => SetValue(ContentProperty, content);

    /// <inheritdoc />
    protected override void OnContentPropertyChanged(IView? oldView, IView? newView)
    {
        if (oldView is VisualElement oldElement)
        {
            oldElement.MeasureInvalidated -= OnContentMeasureInvalidated;
        }

        base.OnContentPropertyChanged(oldView, newView);

        if (newView is VisualElement newElement)
        {
            newElement.MeasureInvalidated += OnContentMeasureInvalidated;
        }
    }

    /// <summary>
    /// Content-driven measure changes must invalidate THIS view's measure.
    /// </summary>
    /// <remarks>
    /// A scroll container is not a <see cref="Layout" />, so nothing subscribes to the content's
    /// invalidations on its behalf: without this, the parent keeps the cached desired size (a
    /// hugging <see cref="SizingStrategy" /> could never grow or shrink) and, on WinUI, the
    /// content panel is never re-measured at all, so the scrollable extent stays stale too.
    /// </remarks>
    private void OnContentMeasureInvalidated(object? sender, EventArgs e) => InvalidateMeasure();

    #endregion

    #region Scrolling behavior properties

    /// <summary>
    /// Bindable property for <see cref="Orientation" /> property.
    /// </summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
        nameof(Orientation),
        typeof(ScrollBoxOrientation),
        typeof(ScrollBox),
        ScrollBoxOrientation.Vertical
    );

    /// <summary>
    /// Gets or sets the scrolling axis. Changing it at runtime is supported and resets the scroll
    /// position to the start of the content.
    /// </summary>
    public ScrollBoxOrientation Orientation
    {
        get => (ScrollBoxOrientation) GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="IsScrollEnabled" /> property.
    /// </summary>
    public static readonly BindableProperty IsScrollEnabledProperty = BindableProperty.Create(
        nameof(IsScrollEnabled),
        typeof(bool),
        typeof(ScrollBox),
        true
    );

    /// <summary>
    /// Gets or sets a value indicating whether user scroll gestures are enabled.
    /// Programmatic scrolling via <see cref="ScrollToAsync(double, double, bool)" /> always works.
    /// </summary>
    public bool IsScrollEnabled
    {
        get => (bool) GetValue(IsScrollEnabledProperty);
        set => SetValue(IsScrollEnabledProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="ScrollBarVisibility" /> property.
    /// </summary>
    public static readonly BindableProperty ScrollBarVisibilityProperty = BindableProperty.Create(
        nameof(ScrollBarVisibility),
        typeof(ScrollBarVisibility),
        typeof(ScrollBox),
        ScrollBarVisibility.Default
    );

    /// <summary>
    /// Gets or sets the scroll bar visibility along the scrolling axis.
    /// </summary>
    /// <remarks>
    /// <see cref="ScrollBarVisibility.Always" /> is honored on Android (fade disabled) and Windows;
    /// on iOS the platform cannot keep indicators permanently visible, so it behaves as
    /// <see cref="ScrollBarVisibility.Default" />.
    /// </remarks>
    public ScrollBarVisibility ScrollBarVisibility
    {
        get => (ScrollBarVisibility) GetValue(ScrollBarVisibilityProperty);
        set => SetValue(ScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="FadingEdgeLength" /> property.
    /// </summary>
    public static readonly BindableProperty FadingEdgeLengthProperty = BindableProperty.Create(
        nameof(FadingEdgeLength),
        typeof(double),
        typeof(ScrollBox),
        0.0
    );

    /// <summary>
    /// Gets or sets the length of the fading edge effect in device-independent units.
    /// </summary>
    /// <remarks>
    /// A value of 0 means no fading edge is applied (default). The fade follows the scrolling
    /// axis. Not supported on Windows.
    /// </remarks>
    public double FadingEdgeLength
    {
        get => (double) GetValue(FadingEdgeLengthProperty);
        set => SetValue(FadingEdgeLengthProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="SizingStrategy" /> property.
    /// </summary>
    public static readonly BindableProperty SizingStrategyProperty = BindableProperty.Create(
        nameof(SizingStrategy),
        typeof(ScrollBoxSizingStrategy),
        typeof(ScrollBox),
        ScrollBoxSizingStrategy.Fill
    );

    /// <summary>
    /// Gets or sets how the box sizes itself along its scrolling axis
    /// (<see cref="ScrollBoxSizingStrategy.Fill" /> by default — the size offered by the parent,
    /// with the content never measured).
    /// </summary>
    /// <remarks>
    /// In XAML: <c>SizingStrategy="Fill"</c>, <c>SizingStrategy="Unbounded"</c>, or a bare number
    /// for a capped hug — <c>SizingStrategy="300"</c> is <see cref="ScrollBoxSizingStrategy.Max(double)" />.
    /// The hugging modes make the box shrink AND grow with its content — the behavior
    /// <see cref="ScrollView" /> is famously unreliable at.
    /// </remarks>
    public ScrollBoxSizingStrategy SizingStrategy
    {
        get => (ScrollBoxSizingStrategy) GetValue(SizingStrategyProperty);
        set => SetValue(SizingStrategyProperty, value);
    }

    #endregion

    #region Read-only scroll state

    private static readonly BindablePropertyKey _scrollXPropertyKey = BindableProperty.CreateReadOnly(
        nameof(ScrollX),
        typeof(double),
        typeof(ScrollBox),
        0.0
    );

    /// <summary>
    /// Bindable property for <see cref="ScrollX" /> property.
    /// </summary>
    public static readonly BindableProperty ScrollXProperty = _scrollXPropertyKey.BindableProperty;

    /// <summary>
    /// Gets the current horizontal scroll position: the distance scrolled from the start of the
    /// content, in device-independent units.
    /// </summary>
    public double ScrollX => (double) GetValue(ScrollXProperty);

    private static readonly BindablePropertyKey _scrollYPropertyKey = BindableProperty.CreateReadOnly(
        nameof(ScrollY),
        typeof(double),
        typeof(ScrollBox),
        0.0
    );

    /// <summary>
    /// Bindable property for <see cref="ScrollY" /> property.
    /// </summary>
    public static readonly BindableProperty ScrollYProperty = _scrollYPropertyKey.BindableProperty;

    /// <summary>
    /// Gets the current vertical scroll position: the distance scrolled from the start of the
    /// content, in device-independent units.
    /// </summary>
    public double ScrollY => (double) GetValue(ScrollYProperty);

    private static readonly BindablePropertyKey _contentSizePropertyKey = BindableProperty.CreateReadOnly(
        nameof(ContentSize),
        typeof(Size),
        typeof(ScrollBox),
        default(Size)
    );

    /// <summary>
    /// Bindable property for <see cref="ContentSize" /> property.
    /// </summary>
    public static readonly BindableProperty ContentSizeProperty = _contentSizePropertyKey.BindableProperty;

    /// <summary>
    /// Gets the size of the scrollable canvas in device-independent units: the measured content
    /// plus <see cref="ViewBoxBase.Padding" />, and never smaller than the viewport.
    /// </summary>
    public Size ContentSize => (Size) GetValue(ContentSizeProperty);

    #endregion

    #region Pull-to-refresh

    /// <summary>
    /// Bindable property for <see cref="IsRefreshEnabled" /> property.
    /// </summary>
    public static readonly BindableProperty IsRefreshEnabledProperty = BindableProperty.Create(
        nameof(IsRefreshEnabled),
        typeof(bool),
        typeof(ScrollBox),
        false
    );

    /// <summary>
    /// Gets or sets a value indicating whether pull-to-refresh is enabled. Not supported on Windows.
    /// </summary>
    public bool IsRefreshEnabled
    {
        get => (bool) GetValue(IsRefreshEnabledProperty);
        set => SetValue(IsRefreshEnabledProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="IsRefreshing" /> property.
    /// </summary>
    public static readonly BindableProperty IsRefreshingProperty = BindableProperty.Create(
        nameof(IsRefreshing),
        typeof(bool),
        typeof(ScrollBox),
        false,
        BindingMode.TwoWay
    );

    /// <summary>
    /// Gets or sets a value indicating whether the refresh indicator is currently showing.
    /// Setting this to true programmatically will show the refresh indicator; setting it to false
    /// will stop it.
    /// </summary>
    public bool IsRefreshing
    {
        get => (bool) GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="RefreshCommand" /> property.
    /// </summary>
    public static readonly BindableProperty RefreshCommandProperty = BindableProperty.Create(
        nameof(RefreshCommand),
        typeof(ICommand),
        typeof(ScrollBox)
    );

    /// <summary>
    /// Gets or sets the command to execute when the user requests a refresh. The command receives
    /// the completion <see cref="Action" /> as its parameter.
    /// </summary>
    public ICommand? RefreshCommand
    {
        get => (ICommand?) GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="RefreshAccentColor" /> property.
    /// </summary>
    public static readonly BindableProperty RefreshAccentColorProperty = BindableProperty.Create(
        nameof(RefreshAccentColor),
        typeof(Color),
        typeof(ScrollBox)
    );

    /// <summary>
    /// Gets or sets the accent color for the refresh indicator.
    /// </summary>
    public Color? RefreshAccentColor
    {
        get => (Color?) GetValue(RefreshAccentColorProperty);
        set => SetValue(RefreshAccentColorProperty, value);
    }

    /// <summary>
    /// Event raised when the user triggers a refresh.
    /// </summary>
    public event EventHandler<ScrollBoxRefreshEventArgs>? OnRefresh;

    #endregion

    #region Scroll events

    /// <summary>
    /// Bindable property for <see cref="ScrolledCommand" /> property.
    /// </summary>
    public static readonly BindableProperty ScrolledCommandProperty = BindableProperty.Create(
        nameof(ScrolledCommand),
        typeof(ICommand),
        typeof(ScrollBox),
        propertyChanged: (bindable, _, _) => ((ScrollBox) bindable).UpdateScrollEventSubscription()
    );

    /// <summary>
    /// Bindable property for <see cref="ScrollStartedCommand" /> property.
    /// </summary>
    public static readonly BindableProperty ScrollStartedCommandProperty = BindableProperty.Create(
        nameof(ScrollStartedCommand),
        typeof(ICommand),
        typeof(ScrollBox),
        propertyChanged: (bindable, _, _) => ((ScrollBox) bindable).UpdateScrollEventSubscription()
    );

    /// <summary>
    /// Bindable property for <see cref="ScrollEndedCommand" /> property.
    /// </summary>
    public static readonly BindableProperty ScrollEndedCommandProperty = BindableProperty.Create(
        nameof(ScrollEndedCommand),
        typeof(ICommand),
        typeof(ScrollBox),
        propertyChanged: (bindable, _, _) => ((ScrollBox) bindable).UpdateScrollEventSubscription()
    );

    /// <summary>
    /// Gets or sets the command to execute when the scroll position changes.
    /// </summary>
    public ICommand? ScrolledCommand
    {
        get => (ICommand?) GetValue(ScrolledCommandProperty);
        set => SetValue(ScrolledCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when a scroll gesture starts.
    /// </summary>
    public ICommand? ScrollStartedCommand
    {
        get => (ICommand?) GetValue(ScrollStartedCommandProperty);
        set => SetValue(ScrollStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when scrolling comes to rest.
    /// </summary>
    public ICommand? ScrollEndedCommand
    {
        get => (ICommand?) GetValue(ScrollEndedCommandProperty);
        set => SetValue(ScrollEndedCommandProperty, value);
    }

    private int _onScrolledSubscriberCount;

    /// <summary>
    /// Event raised when the scroll position changes.
    /// </summary>
    public event EventHandler<ScrollBoxScrolledEventArgs>? OnScrolled
    {
        add
        {
            _onScrolledSubscriberCount++;
            UpdateScrollEventSubscription();
            OnScrolledEvent += value;
        }
        remove
        {
            OnScrolledEvent -= value;
            _onScrolledSubscriberCount--;
            UpdateScrollEventSubscription();
        }
    }

    private event EventHandler<ScrollBoxScrolledEventArgs>? OnScrolledEvent;

    private int _onScrollStartedSubscriberCount;

    /// <summary>
    /// Event raised when a scroll gesture starts moving the content.
    /// </summary>
    public event EventHandler<ScrollBoxScrolledEventArgs>? OnScrollStarted
    {
        add
        {
            _onScrollStartedSubscriberCount++;
            UpdateScrollEventSubscription();
            OnScrollStartedEvent += value;
        }
        remove
        {
            OnScrollStartedEvent -= value;
            _onScrollStartedSubscriberCount--;
            UpdateScrollEventSubscription();
        }
    }

    private event EventHandler<ScrollBoxScrolledEventArgs>? OnScrollStartedEvent;

    private int _onScrollEndedSubscriberCount;

    /// <summary>
    /// Event raised when scrolling comes to rest.
    /// </summary>
    public event EventHandler<ScrollBoxScrolledEventArgs>? OnScrollEnded
    {
        add
        {
            _onScrollEndedSubscriberCount++;
            UpdateScrollEventSubscription();
            OnScrollEndedEvent += value;
        }
        remove
        {
            OnScrollEndedEvent -= value;
            _onScrollEndedSubscriberCount--;
            UpdateScrollEventSubscription();
        }
    }

    private event EventHandler<ScrollBoxScrolledEventArgs>? OnScrollEndedEvent;

    private void UpdateScrollEventSubscription()
    {
        if (Handler is null)
        {
            return;
        }

        var hasSubscribers = ScrolledCommand != null ||
                             ScrollStartedCommand != null ||
                             ScrollEndedCommand != null ||
                             _onScrolledSubscriberCount > 0 ||
                             _onScrollStartedSubscriberCount > 0 ||
                             _onScrollEndedSubscriberCount > 0;

        Handler.Invoke("SetScrollEventEnabled", hasSubscribers);
    }

    #endregion

    #region Programmatic scrolling

    private ScrollBoxScrollToRequest? _pendingHandlerRequest;
    private PendingDescendantScroll? _pendingDescendantScroll;

    private sealed record PendingDescendantScroll(IView View, ScrollToPosition Position, ScrollBoxScrollToRequest Request);

    /// <summary>
    /// Scrolls to the given distances from the start of the content, in device-independent units.
    /// </summary>
    /// <param name="x">Target horizontal distance (ignored for <see cref="ScrollBoxOrientation.Vertical" />).</param>
    /// <param name="y">Target vertical distance (ignored for <see cref="ScrollBoxOrientation.Horizontal" />).</param>
    /// <param name="animated">Whether to animate the scroll.</param>
    /// <returns>
    /// A task that ALWAYS completes: when the scroll settles, immediately when already at the
    /// target, or when a newer scroll request supersedes this one. Requests issued before the
    /// first layout pass are queued and executed right after it (the latest pending request wins).
    /// </returns>
    public Task ScrollToAsync(double x, double y, bool animated = true)
    {
        var request = new ScrollBoxScrollToRequest(x, y, animated);
        SupersedePendingDescendantScroll();
        DispatchScrollToRequest(request);

        return request.Task;
    }

    /// <summary>
    /// Scrolls so that the given descendant view satisfies <paramref name="position" />.
    /// </summary>
    /// <param name="view">A view hosted (directly or indirectly) inside <see cref="Content" />.</param>
    /// <param name="position">Where the view should land in the viewport.</param>
    /// <param name="animated">Whether to animate the scroll.</param>
    /// <returns>
    /// A task that ALWAYS completes (see <see cref="ScrollToAsync(double, double, bool)" />).
    /// Requests issued before the first layout pass resolve the view's position right after it.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="view" /> is not a descendant of this ScrollBox.</exception>
    public Task ScrollToAsync(IView view, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!IsDescendant(view))
        {
            throw new ArgumentException("The view is not a descendant of this ScrollBox.", nameof(view));
        }

        var request = new ScrollBoxScrollToRequest(0, 0, animated);
        SupersedePendingDescendantScroll();

        if (GetGeometryFromHandler() is { ViewportWidth: > 0, ViewportHeight: > 0 } geometry)
        {
            ResolveDescendantTarget(view, position, geometry, request);
            DispatchScrollToRequest(request);
        }
        else
        {
            // Before the first layout pass frames are meaningless: resolve the target after it.
            _pendingDescendantScroll = new PendingDescendantScroll(view, position, request);
        }

        return request.Task;
    }

    private void DispatchScrollToRequest(ScrollBoxScrollToRequest request)
    {
        if (Handler is { } handler)
        {
            _pendingHandlerRequest?.Complete();
            _pendingHandlerRequest = null;
            handler.Invoke("ScrollTo", request);
        }
        else
        {
            // No handler yet: keep the latest request and dispatch it on connection; the platform
            // then applies its own pre-layout queueing.
            _pendingHandlerRequest?.Complete();
            _pendingHandlerRequest = request;
        }
    }

    private void SupersedePendingDescendantScroll()
    {
        if (_pendingDescendantScroll is { } pending)
        {
            _pendingDescendantScroll = null;
            pending.Request.Complete();
        }
    }

    private void FlushPendingDescendantScroll()
    {
        if (_pendingDescendantScroll is not { } pending
            || GetGeometryFromHandler() is not { ViewportWidth: > 0, ViewportHeight: > 0 } geometry)
        {
            return;
        }

        _pendingDescendantScroll = null;

        if (!IsDescendant(pending.View))
        {
            // The view left the tree while the request was queued: nothing sensible to target.
            pending.Request.Complete();

            return;
        }

        ResolveDescendantTarget(pending.View, pending.Position, geometry, pending.Request);
        DispatchScrollToRequest(pending.Request);
    }

    private void ResolveDescendantTarget(IView view, ScrollToPosition position, ScrollBoxGeometry geometry, ScrollBoxScrollToRequest request)
    {
        var (elementX, elementY) = GetPositionInContent(view);

        if (Orientation == ScrollBoxOrientation.Horizontal)
        {
            request.X = ComputeTargetDistance(position, elementX, view.Frame.Width, geometry.ScrollX, geometry.VisibleWidth, geometry.ContentWidth);
            request.Y = geometry.ScrollY;
        }
        else
        {
            request.X = geometry.ScrollX;
            request.Y = ComputeTargetDistance(position, elementY, view.Frame.Height, geometry.ScrollY, geometry.VisibleHeight, geometry.ContentHeight);
        }
    }

    /// <summary>
    /// Computes the target distance-from-content-start for a <see cref="ScrollToPosition" />.
    /// </summary>
    /// <remarks>
    /// MakeVisible ("minimal scroll") is defined relative to <paramref name="referenceDistance" />
    /// — the position the request started from — and the result is clamped to the valid scroll
    /// range, so programmatic scrolls can never under- or over-shoot into the insets.
    /// </remarks>
    internal static double ComputeTargetDistance(
        ScrollToPosition position,
        double elementStart,
        double elementSize,
        double referenceDistance,
        double visibleSize,
        double contentSize
    )
    {
        var maxDistance = Math.Max(0, contentSize - visibleSize);

        var target = position switch
        {
            ScrollToPosition.Center => elementStart + (elementSize / 2) - (visibleSize / 2),
            ScrollToPosition.End => elementStart + elementSize - visibleSize,
            ScrollToPosition.MakeVisible when elementStart >= referenceDistance
                                              && elementStart + elementSize <= referenceDistance + visibleSize
                => referenceDistance,
            ScrollToPosition.MakeVisible when elementStart + elementSize > referenceDistance + visibleSize
                                              && elementStart >= referenceDistance
                => elementStart + elementSize - visibleSize,
            _ => elementStart
        };

        return Math.Clamp(target, 0, maxDistance);
    }

    private bool IsDescendant(IView view)
    {
        var current = view as Element;

        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    /// Accumulates layout frames from the view up to this ScrollBox, yielding the view's position
    /// in the scrollable canvas coordinate space (the content's frame already includes Padding).
    /// </summary>
    private (double X, double Y) GetPositionInContent(IView view)
    {
        double x = 0, y = 0;
        var current = view as Element;

        while (current is not null && !ReferenceEquals(current, this))
        {
            if (current is IView currentView)
            {
                x += currentView.Frame.X;
                y += currentView.Frame.Y;
            }

            current = current.Parent;
        }

        return (x, y);
    }

    private ScrollBoxGeometry? GetGeometryFromHandler()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Handler is ScrollBoxHandler handler)
        {
            return handler.GetGeometry();
        }
#endif
        return null;
    }

    #endregion

    #region IScrollBoxController

    void IScrollBoxController.Refresh(Action completionCallback)
    {
        IsRefreshing = true;

        var wrappedCompletion = () =>
        {
            IsRefreshing = false;
            completionCallback();
        };

        var handled = false;

        if (RefreshCommand is not null)
        {
            handled = true;

            if (RefreshCommand.CanExecute(null))
            {
                RefreshCommand.Execute(wrappedCompletion);
            }
        }

        if (OnRefresh is not null)
        {
            handled = true;
            OnRefresh.Invoke(this, new ScrollBoxRefreshEventArgs(wrappedCompletion));
        }

        if (!handled)
        {
            wrappedCompletion();
        }
    }

    void IScrollBoxController.Scrolled(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        UpdateScrollPosition(scrollX, scrollY);
        var args = new ScrollBoxScrolledEventArgs(scrollX, scrollY, totalScrollableWidth, totalScrollableHeight, _lastWidth, _lastHeight);

        if (ScrolledCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }

        OnScrolledEvent?.Invoke(this, args);
    }

    void IScrollBoxController.ScrollStarted(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        UpdateScrollPosition(scrollX, scrollY);
        var args = new ScrollBoxScrolledEventArgs(scrollX, scrollY, totalScrollableWidth, totalScrollableHeight, _lastWidth, _lastHeight);

        if (ScrollStartedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }

        OnScrollStartedEvent?.Invoke(this, args);
    }

    void IScrollBoxController.ScrollEnded(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        UpdateScrollPosition(scrollX, scrollY);
        var args = new ScrollBoxScrolledEventArgs(scrollX, scrollY, totalScrollableWidth, totalScrollableHeight, _lastWidth, _lastHeight);

        if (ScrollEndedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }

        OnScrollEndedEvent?.Invoke(this, args);
    }

    void IScrollBoxController.ContentLaidOut(double contentWidth, double contentHeight)
    {
        SetValue(_contentSizePropertyKey, new Size(contentWidth, contentHeight));
        FlushPendingDescendantScroll();
    }

    internal void UpdateScrollPosition(double scrollX, double scrollY)
    {
        SetValue(_scrollXPropertyKey, scrollX);
        SetValue(_scrollYPropertyKey, scrollY);
    }

    #endregion

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateScrollEventSubscription();

        if (Handler is not null && _pendingHandlerRequest is { } request)
        {
            _pendingHandlerRequest = null;
            Handler.Invoke("ScrollTo", request);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        _lastWidth = width;
        _lastHeight = height;
    }
}
