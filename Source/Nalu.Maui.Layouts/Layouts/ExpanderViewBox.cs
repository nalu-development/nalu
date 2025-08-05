using Microsoft.Maui.Layouts;
using ScheduledAnimation = (double originalWidth, double originalHeight, double deltaWidth, double deltaHeight);

namespace Nalu;

/// <summary>
/// Represents a view box that can be expanded or collapsed.
/// </summary>
public interface IExpanderViewBox : IViewBox
{
    /// <summary>
    /// Gets the width of the view box when it is collapsed.
    /// </summary>
    /// <remarks>
    /// -1 means the expander does not collapse horizontally.
    /// </remarks>
    double CollapsedWidth { get; }

    /// <summary>
    /// Gets the height of the view box when it is collapsed.
    /// </summary>
    /// <remarks>
    /// -1 means the expander does not collapse vertically.
    /// </remarks>
    double CollapsedHeight { get; }

    /// <summary>
    /// Gets the expand state of the view box.
    /// </summary>
    bool IsExpanded { get; }

    /// <summary>
    /// Gets the current arrange width of the view box.
    /// </summary>
    double ArrangeWidth { get; }

    /// <summary>
    /// Gets the current arrange height of the view box.
    /// </summary>
    double ArrangeHeight { get; }

    /// <summary>
    /// Sets the target arrange size of the view box.
    /// </summary>
    /// <param name="width">The target <see cref="ArrangeWidth" />.</param>
    /// <param name="height">The target <see cref="ArrangeHeight" />.</param>
    /// <param name="willCollapse">Whether the content will collapse when the view box is not expanded.</param>
    void SetArrangeSize(double width, double height, bool willCollapse);
}

/// <summary>
/// Describes the behavior of the expander animation.
/// </summary>
public enum ExpanderAnimationBehavior
{
    /// <summary>
    /// When appearing for the first time and when <see cref="BindableObject.BindingContext" /> changes, the view box won't animate.
    /// </summary>
    /// <remarks>
    /// This is the default behavior as it serves to properly handle collection views.
    /// </remarks>
    Default,

    /// <summary>
    /// When <see cref="BindableObject.BindingContext" /> changes, the view box will animate.
    /// </summary>
    /// <remarks>
    /// When appearing for the first time, the view box won't animate.
    /// </remarks>
    AnimateOnBindingContextChange,

    /// <summary>
    /// When appearing for the first time and when <see cref="BindableObject.BindingContext" /> changes, the view box will animate.
    /// </summary>
    Always
}

/// <summary>
/// Represents a view box that can be expanded or collapsed with an animation.
/// </summary>
[ContentProperty(nameof(Content))]
public class ExpanderViewBox : ViewBoxBase, IExpanderViewBox, IDisposable
{
#pragma warning disable IDE1006
    // ReSharper disable once InconsistentNaming
    private static readonly BindablePropertyKey CanCollapsePropertyKey = BindableProperty.CreateReadOnly(
        nameof(CanCollapse),
        typeof(bool),
        typeof(ExpanderViewBox),
        false
    );
#pragma warning restore IDE1006

    /// <summary>
    /// Bindable property for <see cref="AnimationBehavior" /> property.
    /// </summary>
    public static readonly BindableProperty AnimationBehaviorProperty = BindableProperty.Create(
        nameof(AnimationBehavior),
        typeof(ExpanderAnimationBehavior),
        typeof(ExpanderViewBox),
        ExpanderAnimationBehavior.Default
    );

    /// <summary>
    /// Bindable property for <see cref="Content" /> property.
    /// </summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(IView),
        typeof(ViewBox),
        propertyChanged: OnContentPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="CanCollapse" /> property.
    /// </summary>
    public static readonly BindableProperty CanCollapseProperty = CanCollapsePropertyKey.BindableProperty;

    /// <summary>
    /// Bindable property for <see cref="CollapsedWidth" /> property.
    /// </summary>
    public static readonly BindableProperty CollapsedWidthProperty = BindableProperty.Create(
        nameof(CollapsedWidth),
        typeof(double),
        typeof(ExpanderViewBox),
        -1.0,
        propertyChanged: OnExpanderPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="CollapsedHeight" /> property.
    /// </summary>
    public static readonly BindableProperty CollapsedHeightProperty = BindableProperty.Create(
        nameof(CollapsedHeight),
        typeof(double),
        typeof(ExpanderViewBox),
        -1.0,
        propertyChanged: OnExpanderPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="IsExpanded" /> property.
    /// </summary>
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded),
        typeof(bool),
        typeof(ExpanderViewBox),
        true,
        propertyChanged: OnExpanderPropertyChanged
    );

    private double _arrangeHeight;
    private double _arrangeWidth;
    private double _lastArrangeHeight;
    private double _lastArrangeWidth;
    private bool _canCollapse;
    private bool _arranged;
    private Animation? _animation;
    private bool _scheduledCanCollapse;
    private ScheduledAnimation? _scheduledAnimation;

    bool IViewBox.ClipsToBounds => true;
    double IExpanderViewBox.ArrangeHeight => _arrangeHeight;
    double IExpanderViewBox.ArrangeWidth => _arrangeWidth;

    /// <summary>
    /// Gets or sets the content of the view box.
    /// </summary>
    public IView? Content
    {
        get => GetContent();
        set => SetContent(value);
    }

    /// <summary>
    /// Gets or sets the behavior of the expander animation.
    /// </summary>
    public ExpanderAnimationBehavior AnimationBehavior
    {
        get => (ExpanderAnimationBehavior) GetValue(AnimationBehaviorProperty);
        set => SetValue(AnimationBehaviorProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the content will collapse when the view box is not expanded.
    /// </summary>
    public bool CanCollapse
    {
        get => (bool) GetValue(CanCollapsePropertyKey.BindableProperty);
        private set => SetValue(CanCollapsePropertyKey, value);
    }

    /// <summary>
    /// Gets the width of the view box when it is collapsed horizontally.
    /// </summary>
    /// <remarks>
    /// -1 means the expander does not collapse horizontally.
    /// </remarks>
    public double CollapsedWidth
    {
        get => (double) GetValue(CollapsedWidthProperty);
        set => SetValue(CollapsedWidthProperty, value);
    }

    /// <summary>
    /// Gets the height of the view box when it is collapsed vertically.
    /// </summary>
    /// <remarks>
    /// -1 means the expander does not collapse vertically.
    /// </remarks>
    public double CollapsedHeight
    {
        get => (double) GetValue(CollapsedHeightProperty);
        set => SetValue(CollapsedHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the view box is expanded.
    /// </summary>
    /// <remarks>
    /// The view box is expanded by default.
    /// </remarks>
    public bool IsExpanded
    {
        get => (bool) GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new ExpanderViewBoxLayoutManager(this);

    /// <inheritdoc />
    protected override IView? GetContent() => (IView?) GetValue(ContentProperty);

    /// <inheritdoc />
    protected override void SetContent(IView? content) => SetValue(ContentProperty, content);

    void IExpanderViewBox.SetArrangeSize(double width, double height, bool canCollapse)
    {
        _scheduledCanCollapse = canCollapse;

        // Fast path: if the target size is the same as the current one, do nothing.
        if (_lastArrangeHeight == height && _lastArrangeWidth == width)
        {
            return;
        }

        _lastArrangeWidth = width;
        _lastArrangeHeight = height;

        if ((!IsPlatformEnabled || !_arranged) && AnimationBehavior != ExpanderAnimationBehavior.Always)
        {
            _arrangeWidth = width;
            _arrangeHeight = height;
            if (_canCollapse != _scheduledCanCollapse)
            {
                _canCollapse = _scheduledCanCollapse;
                CanCollapse = _scheduledCanCollapse;
            }

            InvalidateMeasure();

            return;
        }

        var originalWidth = _arrangeWidth;
        var originalHeight = _arrangeHeight;
        var deltaWidth = width - originalWidth;
        var deltaHeight = height - originalHeight;

        _scheduledAnimation = (originalWidth, originalHeight, deltaWidth, deltaHeight);
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            ClearArrangeData();
        }
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        if (AnimationBehavior == ExpanderAnimationBehavior.Default)
        {
            // Do not animate when the binding context changes (usually when the view is reused within collection view).
            ClearArrangeData();
        }

        base.OnBindingContextChanged();
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Rect bounds)
    {
        _arranged = true;

        // Using private field here to avoid setting the bindable property on each method call (performance).
        if (_canCollapse != _scheduledCanCollapse)
        {
            _canCollapse = _scheduledCanCollapse;
            CanCollapse = _scheduledCanCollapse;
        }

        if (_scheduledAnimation is { } scheduledAnimation)
        {
            _scheduledAnimation = null;
            var (originalWidth, originalHeight, deltaWidth, deltaHeight) = scheduledAnimation;

            _animation?.Dispose();

            _animation = new Animation(
                ratio =>
                {
                    _arrangeWidth = originalWidth + (deltaWidth * ratio);
                    _arrangeHeight = originalHeight + (deltaHeight * ratio);
                    InvalidateMeasure();
                },
                easing: Easing.CubicInOut
            );

            _animation.Commit(this, nameof(IsExpanded));
        }

        return base.ArrangeOverride(bounds);
    }

    private void ClearArrangeData()
    {
        _lastArrangeHeight = 0;
        _lastArrangeWidth = 0;
        _arrangeHeight = 0;
        _arrangeWidth = 0;
        _scheduledAnimation = null;
        _arranged = false;
    }

    private static void OnExpanderPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ExpanderViewBox expanderViewBox)
        {
            expanderViewBox.InvalidateMeasure();
        }
    }

    private static void OnContentPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((ExpanderViewBox) bindable).OnContentPropertyChanged((IView?) oldValue, (IView?) newValue);

    /// <inheritdoc cref="Dispose()" />
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
