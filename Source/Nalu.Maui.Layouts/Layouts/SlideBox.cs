using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// A pager presenting one of an ordered set of lazily-realized, state-retaining slides, with
/// animated index navigation, optional interactive swiping and neighbor peeking.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="SlideBoxItem" />'s template is realized on first presentation and retained
/// forever after — slide state survives navigation. Disabling an item excludes it from the
/// sequence and tears its content down (re-enabling rebuilds lazily).
/// </para>
/// <para>
/// With a non-zero <see cref="PeekAreaInsets" /> the adjacent enabled slides stay partially
/// visible at rest (and are therefore realized eagerly); with no peek they materialize only
/// when presented or when a swipe starts.
/// </para>
/// </remarks>
[ContentProperty(nameof(Items))]
public class SlideBox : Layout
{
    private const string _transitionAnimationName = "NaluSlideBoxTransition";

    /// <summary>Bindable property for <see cref="SelectedIndex" />.</summary>
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(SlideBox),
        0,
        BindingMode.TwoWay,
        coerceValue: static (bindable, value) => ((SlideBox) bindable).CoerceIndex((int) value),
        propertyChanged: static (bindable, oldValue, newValue) => ((SlideBox) bindable).OnSelectedIndexChanged((int) oldValue, (int) newValue)
    );

    private static readonly BindablePropertyKey _selectedItemPropertyKey = BindableProperty.CreateReadOnly(
        nameof(SelectedItem),
        typeof(SlideBoxItem),
        typeof(SlideBox),
        null
    );

    /// <summary>Bindable property for <see cref="SelectedItem" />.</summary>
    public static readonly BindableProperty SelectedItemProperty = _selectedItemPropertyKey.BindableProperty;

    /// <summary>Bindable property for <see cref="IsSwipeEnabled" />.</summary>
    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(
        nameof(IsSwipeEnabled),
        typeof(bool),
        typeof(SlideBox),
        true
    );

    /// <summary>Bindable property for <see cref="Orientation" />.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
        nameof(Orientation),
        typeof(SlideBoxOrientation),
        typeof(SlideBox),
        SlideBoxOrientation.Horizontal,
        propertyChanged: static (bindable, _, _) =>
        {
            var slideBox = (SlideBox) bindable;
            slideBox.ApplyOrientationSafeAreaDefaults();
            slideBox.OnStructureChanged();
        }
    );

    /// <summary>Bindable property for <see cref="PeekAreaInsets" />.</summary>
    public static readonly BindableProperty PeekAreaInsetsProperty = BindableProperty.Create(
        nameof(PeekAreaInsets),
        typeof(Thickness),
        typeof(SlideBox),
        default(Thickness),
        propertyChanged: static (bindable, _, _) => ((SlideBox) bindable).OnStructureChanged()
    );

    /// <summary>Bindable property for <see cref="TransitionDuration" />.</summary>
    public static readonly BindableProperty TransitionDurationProperty = BindableProperty.Create(
        nameof(TransitionDuration),
        typeof(uint),
        typeof(SlideBox),
        250u
    );

    /// <summary>Bindable property for <see cref="TransitionEasing" />.</summary>
    public static readonly BindableProperty TransitionEasingProperty = BindableProperty.Create(
        nameof(TransitionEasing),
        typeof(Easing),
        typeof(SlideBox),
        Easing.CubicOut
    );

    /// <summary>Gets the slides.</summary>
    public IList<SlideBoxItem> Items { get; }

    /// <summary>
    /// Gets or sets the selected slide index within the FULL item list. Values pointing at a
    /// disabled item are coerced to the nearest enabled one (-1 when no enabled item exists).
    /// </summary>
    public int SelectedIndex
    {
        get => (int) GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Gets the currently selected item, or null.</summary>
    public SlideBoxItem? SelectedItem => (SlideBoxItem?) GetValue(SelectedItemProperty);

    /// <summary>Gets or sets whether the user can swipe between adjacent slides.</summary>
    public bool IsSwipeEnabled
    {
        get => (bool) GetValue(IsSwipeEnabledProperty);
        set => SetValue(IsSwipeEnabledProperty, value);
    }

    /// <summary>Gets or sets the sliding axis.</summary>
    public SlideBoxOrientation Orientation
    {
        get => (SlideBoxOrientation) GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets how much of the adjacent slides stays visible at rest
    /// (Left/Right for <see cref="SlideBoxOrientation.Horizontal" />, Top/Bottom for vertical).
    /// </summary>
    public Thickness PeekAreaInsets
    {
        get => (Thickness) GetValue(PeekAreaInsetsProperty);
        set => SetValue(PeekAreaInsetsProperty, value);
    }

    /// <summary>Gets or sets the slide transition duration in milliseconds.</summary>
    public uint TransitionDuration
    {
        get => (uint) GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <summary>Gets or sets the slide transition easing.</summary>
    public Easing TransitionEasing
    {
        get => (Easing) GetValue(TransitionEasingProperty);
        set => SetValue(TransitionEasingProperty, value);
    }

    /// <summary>Raised after the selection changed (before the transition animation completes).</summary>
    public event EventHandler<SlideBoxSelectionChangedEventArgs>? SelectedIndexChanged;

    private readonly PanGestureRecognizer _pan = new();
    private double _dragOffset;
    private bool _dragging;

    /// <summary>Initializes a new instance of the <see cref="SlideBox" /> class.</summary>
    public SlideBox()
    {
        IsClippedToBounds = true;
        ApplyOrientationSafeAreaDefaults();

        var items = new ObservableCollection<SlideBoxItem>();
        items.CollectionChanged += OnItemsChanged;
        Items = items;

        _pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(_pan);
    }

    /// <summary>
    /// Safe area is consumed on the SLIDING AXIS only (the page slot and peek bands must not
    /// hide under a notch), while the cross-axis insets flow through untouched — handling them
    /// belongs to the slide templates (e.g. full-bleed backgrounds under the system bars).
    /// Re-applied whenever <see cref="Orientation" /> changes; assign your own
    /// <c>SafeAreaEdges</c> afterwards to override.
    /// </summary>
    /// <remarks>
    /// The per-edge safe-area API only exists on .NET 10 / MAUI 10 — on MAUI 9 the platform
    /// default behavior applies unchanged.
    /// </remarks>
    private void ApplyOrientationSafeAreaDefaults()
    {
#if NET10_0_OR_GREATER
        SafeAreaEdges = Orientation == SlideBoxOrientation.Horizontal
            ? new SafeAreaEdges(SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None)
            : new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.Container);
#endif
    }

    /// <summary>Moves to the nearest enabled slide after the current one. Returns false at the end.</summary>
    public bool Next() => Step(1);

    /// <summary>Moves to the nearest enabled slide before the current one. Returns false at the start.</summary>
    public bool Previous() => Step(-1);

    private bool Step(int direction)
    {
        var target = FindEnabled(SelectedIndex, direction);

        if (target < 0)
        {
            return false;
        }

        SelectedIndex = target;

        return true;
    }

    /// <summary>Finds the nearest enabled index strictly beyond <paramref name="from" /> in <paramref name="direction" />, or -1.</summary>
    private int FindEnabled(int from, int direction)
    {
        for (var i = from + direction; i >= 0 && i < Items.Count; i += direction)
        {
            if (Items[i].IsEnabled)
            {
                return i;
            }
        }

        return -1;
    }

    private object CoerceIndex(int requested)
    {
        if (Items.Count == 0)
        {
            return -1;
        }

        var index = Math.Clamp(requested, 0, Items.Count - 1);

        if (Items[index].IsEnabled)
        {
            return index;
        }

        // Nearest enabled, forward preferred on ties.
        for (var distance = 1; distance < Items.Count; distance++)
        {
            if (index + distance < Items.Count && Items[index + distance].IsEnabled)
            {
                return index + distance;
            }

            if (index - distance >= 0 && Items[index - distance].IsEnabled)
            {
                return index - distance;
            }
        }

        return -1;
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        var oldItem = oldIndex >= 0 && oldIndex < Items.Count ? Items[oldIndex] : null;
        var newItem = newIndex >= 0 && newIndex < Items.Count ? Items[newIndex] : null;
        SetValue(_selectedItemPropertyKey, newItem);

        Present(animated: true, direction: Math.Sign(newIndex - oldIndex));

        SelectedIndexChanged?.Invoke(this, new SlideBoxSelectionChangedEventArgs(oldIndex, newIndex, oldItem, newItem));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (SlideBoxItem item in e.OldItems)
            {
                TearDown(item);
                RemoveLogicalChild(item);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (SlideBoxItem item in e.NewItems)
            {
                AddLogicalChild(item);
            }
        }

        // Re-coerce: indexes may have shifted, the selection may have appeared or vanished.
        // SetValue would no-op on an unchanged number, so the coerced companions are synced
        // explicitly (the initial "index 0 exists now" case never raises a change).
        var coerced = (int) CoerceIndex(SelectedIndex < 0 ? 0 : SelectedIndex)!;

        if (coerced != SelectedIndex)
        {
            SelectedIndex = coerced;
        }
        else
        {
            SetValue(_selectedItemPropertyKey, coerced >= 0 ? Items[coerced] : null);
            Present(animated: false);
        }
    }

    internal void OnItemTemplateChanged(SlideBoxItem item)
    {
        // A replaced template invalidates the realized content: rebuild on the next visit.
        TearDown(item);
        Present(animated: false);
    }

    internal void OnItemIsEnabledChanged(SlideBoxItem item)
    {
        if (!item.IsEnabled)
        {
            TearDown(item);
        }

        // Selecting the same number re-runs coercion: a disabled selection moves to the
        // nearest enabled slide, and a first-enabled item resurrects a -1 selection.
        var target = (int) (CoerceIndex(SelectedIndex is -1 ? Items.IndexOf(item) : SelectedIndex))!;

        if (target != SelectedIndex)
        {
            SelectedIndex = target;
        }
        else
        {
            Present(animated: false);
        }
    }

    private void TearDown(SlideBoxItem item)
    {
        if (item.Content is { } content)
        {
            item.Content = null;
            Remove(content);
            content.DisconnectHandlers();
        }
    }

    private void EnsureContent(SlideBoxItem item)
    {
        if (item.Content is not null || item.Template is null || !item.IsEnabled)
        {
            return;
        }

        var content = (View) item.Template.CreateContent();
        content.IsVisible = false;
        item.Content = content;
        Add(content);
    }

    /// <summary>Signed count of enabled steps from the selected item to <paramref name="index" /> (disabled items don't exist visually).</summary>
    private int EnabledDistance(int selectedIndex, int index)
    {
        if (index == selectedIndex)
        {
            return 0;
        }

        var direction = index > selectedIndex ? 1 : -1;
        var distance = 0;

        for (var i = selectedIndex + direction; i != index + direction; i += direction)
        {
            if (Items[i].IsEnabled)
            {
                distance += direction;
            }
        }

        return distance;
    }

    private bool IsHorizontal => Orientation == SlideBoxOrientation.Horizontal;

    private bool IsRtl => IsHorizontal && ((IView) this).FlowDirection == FlowDirection.RightToLeft;

    internal double PageSize
    {
        get
        {
            var peek = PeekAreaInsets;

            return IsHorizontal
                ? Width - Padding.HorizontalThickness - peek.Left - peek.Right
                : Height - Padding.VerticalThickness - peek.Top - peek.Bottom;
        }
    }

    private double RestTranslation(int enabledDistance)
        => enabledDistance * PageSize * (IsRtl ? -1 : 1);

    private void SetTranslation(View view, double value)
    {
        if (IsHorizontal)
        {
            view.TranslationX = value;
        }
        else
        {
            view.TranslationY = value;
        }
    }

    private double GetTranslation(View view) => IsHorizontal ? view.TranslationX : view.TranslationY;

    private bool PeeksTowards(int enabledDistance)
    {
        var peek = PeekAreaInsets;

        if (IsHorizontal)
        {
            return enabledDistance * (IsRtl ? -1 : 1) > 0 ? peek.Right > 0 : peek.Left > 0;
        }

        return enabledDistance > 0 ? peek.Bottom > 0 : peek.Top > 0;
    }

    private void OnStructureChanged()
    {
        // The page slot geometry changed: child frames must be re-arranged (translations
        // alone don't cover it — without this, removing the peek would leave the current
        // slide at its narrowed frame).
        InvalidateMeasure();
        Present(animated: false);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Rect bounds)
    {
        var result = base.ArrangeOverride(bounds);

        // First real arrange (or a resize): rest positions depend on the page size. A layout
        // pass during a running transition must NOT snap it dead.
        if (!this.AnimationIsRunning(_transitionAnimationName))
        {
            Present(animated: false, keepDrag: true);
        }

        return result;
    }

    /// <summary>
    /// Realizes what must exist, positions every realized slide at its rest translation
    /// (plus the active drag offset) and hides everything beyond the participating window.
    /// </summary>
    /// <param name="animated">Whether to animate towards the rest positions.</param>
    /// <param name="keepDrag">Whether the active drag offset applies on top of the rest positions.</param>
    /// <param name="direction">Travel direction hint: entering views start one page towards it.</param>
    private void Present(bool animated, bool keepDrag = false, int direction = 0)
    {
        if (Items.Count == 0 || PageSize <= 0)
        {
            return;
        }

        var selected = SelectedIndex;

        if (selected < 0)
        {
            foreach (var item in Items)
            {
                if (item.Content is { } content)
                {
                    content.IsVisible = false;
                }
            }

            return;
        }

        var drag = keepDrag && _dragging ? _dragOffset : 0;

        // Realize the selection plus the neighbors that peek into view.
        EnsureContent(Items[selected]);

        var next = FindEnabled(selected, 1);
        var previous = FindEnabled(selected, -1);

        if (next >= 0 && (_dragging || PeeksTowards(1)))
        {
            EnsureContent(Items[next]);
        }

        if (previous >= 0 && (_dragging || PeeksTowards(-1)))
        {
            EnsureContent(Items[previous]);
        }

        this.AbortAnimation(_transitionAnimationName);

        var animation = animated && Handler is not null && TransitionDuration > 0 ? new Animation() : null;
        List<View>? toHide = null;

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Content is not { } view)
            {
                continue;
            }

            var distance = EnabledDistance(selected, i);

            if (Math.Abs(distance) <= 1)
            {
                var target = RestTranslation(distance) + drag;

                if (animation is null)
                {
                    SetTranslation(view, target);
                    view.IsVisible = true;
                }
                else
                {
                    // An entering view starts one page towards the travel direction so every
                    // transition travels at most one page visually (a backwards navigation
                    // slides in from the previous side, not always from the next).
                    if (!view.IsVisible)
                    {
                        var entrySide = direction != 0 ? direction : Math.Sign(distance) is 0 ? 1 : Math.Sign(distance);
                        SetTranslation(view, RestTranslation(distance + entrySide));
                        view.IsVisible = true;
                    }

                    var from = GetTranslation(view);

                    if (Math.Abs(from - target) < 0.5)
                    {
                        SetTranslation(view, target);
                    }
                    else
                    {
                        var capturedView = view;
                        animation.Add(0, 1, new Animation(v => SetTranslation(capturedView, v), from, target));
                    }
                }
            }
            else if (view.IsVisible)
            {
                if (animation is null)
                {
                    view.IsVisible = false;
                    SetTranslation(view, 0);
                }
                else
                {
                    // Slide one page towards its side, then hide.
                    var from = GetTranslation(view);
                    var target = RestTranslation(Math.Sign(distance) * 2);
                    var capturedView = view;
                    animation.Add(0, 1, new Animation(v => SetTranslation(capturedView, v), from, target));
                    (toHide ??= []).Add(view);
                }
            }
        }

        if (animation is not null)
        {
            animation.Commit(
                this,
                _transitionAnimationName,
                length: TransitionDuration,
                easing: TransitionEasing,
                finished: (_, canceled) =>
                {
                    if (canceled || toHide is null)
                    {
                        return;
                    }

                    foreach (var view in toHide)
                    {
                        view.IsVisible = false;
                        SetTranslation(view, 0);
                    }
                }
            );
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsSwipeEnabled || SelectedIndex < 0 || PageSize <= 0)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                this.AbortAnimation(_transitionAnimationName);
                _dragging = true;
                _dragOffset = 0;

                break;

            case GestureStatus.Running when _dragging:
            {
                var total = IsHorizontal ? e.TotalX : e.TotalY;

                // One page per gesture; rubber-band with no target beyond the edge.
                var direction = total < 0 ? 1 : -1;
                var logicalDirection = IsRtl ? -direction : direction;
                var hasTarget = FindEnabled(SelectedIndex, logicalDirection) >= 0;
                var limit = PageSize;
                total = Math.Clamp(total, -limit, limit);

                _dragOffset = hasTarget ? total : total * 0.25;

                Present(animated: false, keepDrag: true);

                break;
            }

            case GestureStatus.Completed or GestureStatus.Canceled when _dragging:
            {
                var offset = _dragOffset;
                _dragging = false;
                _dragOffset = 0;

                var direction = offset < 0 ? 1 : -1;
                var logicalDirection = IsRtl ? -direction : direction;
                var target = FindEnabled(SelectedIndex, logicalDirection);
                var commit = e.StatusType == GestureStatus.Completed
                             && target >= 0
                             && Math.Abs(offset) > PageSize / 3;

                if (commit)
                {
                    // Present (from the current dragged translations) animates the landing.
                    SelectedIndex = target;

                    if (SelectedIndex != target)
                    {
                        Present(animated: true);
                    }
                }
                else
                {
                    Present(animated: true);
                }

                break;
            }
        }
    }

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new SlideBoxLayoutManager(this);
}
