using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// One resting height of a bottom sheet. Create via <see cref="Content"/> (the sheet hugs its
/// content's measured height), <see cref="Fraction"/> (of the available presentation height) or
/// <see cref="Height"/> (absolute device-independent units). Every form is clamped to the
/// available height.
/// </summary>
public readonly struct ScaffoldSheetDetent
{
    private enum DetentKind : byte
    {
        Content,
        Fraction,
        Height
    }

    private readonly DetentKind _kind;
    private readonly double _value;

    private ScaffoldSheetDetent(DetentKind kind, double value)
    {
        _kind = kind;
        _value = value;
    }

    /// <summary>A detent hugging the sheet content's measured height.</summary>
    public static ScaffoldSheetDetent Content { get; } = new(DetentKind.Content, 0);

    /// <summary>A detent at the given fraction (0..1) of the available presentation height.</summary>
    public static ScaffoldSheetDetent Fraction(double fraction) => new(DetentKind.Fraction, fraction);

    /// <summary>A detent at the given absolute height in device-independent units.</summary>
    public static ScaffoldSheetDetent Height(double height) => new(DetentKind.Height, height);

    /// <summary>Resolves the visible sheet height for this detent.</summary>
    internal double Resolve(double availableHeight, double contentHeight)
        => Math.Clamp(
            _kind switch
            {
                DetentKind.Fraction => availableHeight * _value,
                DetentKind.Height => _value,
                _ => contentHeight
            },
            0,
            availableHeight
        );
}

/// <summary>
/// Presentation options of <see cref="Scaffold.ShowBottomSheetAsync"/>. The sheet renders in the
/// top overlay layer, slides from the bottom edge over any chrome, and handles drag between
/// detents and pull-down-to-close at the virtual view layer (no native sheet involved).
/// </summary>
public sealed class ScaffoldBottomSheetOptions
{
    /// <summary>Gets or sets the scrim brush behind the sheet (gradients supported). Defaults to a theme-aware translucent black.</summary>
    public Brush? Scrim { get; init; }

    /// <summary>Gets or sets whether tapping the scrim closes the sheet. Defaults to true.</summary>
    public bool CloseOnScrimTap { get; init; } = true;

    /// <summary>
    /// Gets or sets whether the system back gesture closes the sheet. Defaults to true; when
    /// false, back is consumed without closing while the sheet is topmost.
    /// </summary>
    public bool CloseOnBack { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum sheet width. Defaults to unbounded (full window width); when
    /// the window is wider (tablets, landscape), the sheet floats centered at this width,
    /// still bottom-anchored.
    /// </summary>
    public double MaxWidth { get; init; } = double.PositiveInfinity;

    /// <summary>Gets or sets the resting heights. Defaults to a single <see cref="ScaffoldSheetDetent.Content"/> detent.</summary>
    public ScaffoldSheetDetent[] Detents { get; init; } = [ScaffoldSheetDetent.Content];

    /// <summary>Gets or sets the index (into <see cref="Detents"/>) the sheet opens at.</summary>
    public int InitialDetent { get; init; }

    /// <summary>Gets or sets whether dragging below the smallest detent dismisses the sheet. Defaults to true.</summary>
    public bool AllowPullDownToClose { get; init; } = true;

    /// <summary>Gets or sets whether the built-in drag handle is shown. Defaults to true.</summary>
    public bool ShowDragHandle { get; init; } = true;
}

/// <summary>
/// The bottom sheet chrome of <see cref="Scaffold.ShowBottomSheetAsync"/>: a top-rounded surface
/// with an optional drag handle hosting the user content, owning the drag gesture, detent
/// snapping and pull-down-to-close entirely at the virtual view layer.
/// </summary>
/// <remarks>
/// <para>
/// The drag rides the WHOLE sheet surface: inner controls that need their own drag gestures
/// prevent propagation themselves (e.g. <c>InteractableCanvasView.StopPropagation</c>), and
/// scrollable content relies on platform gesture arbitration. The sheet is as tall as its
/// LARGEST detent — dragging up past it is a no-op (expansion exists only while a bigger detent
/// does), dragging far enough below the smallest detent dismisses (when allowed).
/// </para>
/// <para>
/// Instances are created per presentation — the type is public as a styling surface (and for
/// <see cref="SnapToDetentAsync"/>):
/// <code>
/// &lt;Style TargetType="nalu:ScaffoldBottomSheetView"&gt;
///     &lt;Setter Property="SheetBackground" Value="{AppThemeBinding Light=..., Dark=...}" /&gt;
/// &lt;/Style&gt;
/// </code>
/// </para>
/// </remarks>
public sealed class ScaffoldBottomSheetView : Border
{
    private const double _dismissThreshold = 56;
    private const uint _animationDuration = 250;

    private readonly ScaffoldBottomSheetOptions _options;
    private readonly RoundRectangle _handle;

    private double[] _detentOffsets = [0];
    private double _sheetHeight;
    private double _panStartTranslationY;
    private bool _panning;
    private Func<Task>? _dismissAsync;

    // Null-conditionals below: implicit styles apply from the VisualElement base ctor, before
    // the subviews exist; the ctor seeds the final values.

    /// <summary>Bindable property for <see cref="SheetBackground"/>.</summary>
    public static readonly BindableProperty SheetBackgroundProperty =
        GenericBindableProperty<ScaffoldBottomSheetView>.Create<Brush?>(
            nameof(SheetBackground),
            defaultValueCreator: static _ => new SolidColorBrush(Colors.White),
            propertyChanged: static sheet => (_, value) => sheet.Background = value
        );

    /// <summary>Bindable property for <see cref="SheetCornerRadius"/>.</summary>
    public static readonly BindableProperty SheetCornerRadiusProperty =
        GenericBindableProperty<ScaffoldBottomSheetView>.Create(
            nameof(SheetCornerRadius),
            16.0,
            propertyChanged: static sheet => (_, value) => sheet.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(value, value, 0, 0) }
        );

    /// <summary>Bindable property for <see cref="HandleColor"/>.</summary>
    public static readonly BindableProperty HandleColorProperty =
        GenericBindableProperty<ScaffoldBottomSheetView>.Create(
            nameof(HandleColor),
            Color.FromArgb("#4D8E8E93"),
            propertyChanged: static sheet => (_, value) => sheet._handle?.Fill = new SolidColorBrush(value)
        );

    /// <summary>
    /// Gets or sets the sheet surface brush. Drives the view's own
    /// <see cref="VisualElement.Background"/> — style THIS, not <c>Background</c>.
    /// </summary>
    public Brush? SheetBackground
    {
        get => (Brush?)GetValue(SheetBackgroundProperty);
        set => SetValue(SheetBackgroundProperty, value);
    }

    /// <summary>Gets or sets the radius of the sheet's top corners.</summary>
    public double SheetCornerRadius
    {
        get => (double)GetValue(SheetCornerRadiusProperty);
        set => SetValue(SheetCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the drag handle color.</summary>
    public Color HandleColor
    {
        get => (Color)GetValue(HandleColorProperty);
        set => SetValue(HandleColorProperty, value);
    }

    internal ScaffoldBottomSheetView(View content, ScaffoldBottomSheetOptions options)
    {
        _options = options;
        StrokeThickness = 0;
        AutomationId = "ScaffoldBottomSheet";

        // Overlay views never self-inset: the presenter owns the inset math, and the net10
        // inset listener would otherwise pad the sheet by its system-bar overlap (content
        // displaced and cut on Android).
        SafeAreaEdges = SafeAreaEdges.None;

        _handle = new RoundRectangle
        {
            WidthRequest = 36,
            HeightRequest = 4,
            CornerRadius = new CornerRadius(2),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = options.ShowDragHandle
        };

        var layout = new Grid
        {
            // Same trap as the tab bar (see ScaffoldTabBarView): a MAUI Grid self-pads from the
            // root window insets on Android before it knows its real window position — an
            // overlay-mounted grid gets offset by the status bar. Insets are the presenter's job.
            SafeAreaEdges = SafeAreaEdges.None,
            RowDefinitions =
            {
                new RowDefinition(options.ShowDragHandle ? new GridLength(20) : new GridLength(0)),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(_handle, 0, 0);
        layout.Add(content, 0, 1);

        Content = layout;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(pan);

        // Defaults never raise propertyChanged: seed once from the current values.
        Background = SheetBackground;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCornerRadius, SheetCornerRadius, 0, 0) };
        _handle.Fill = new SolidColorBrush(HandleColor);
    }

    /// <summary>
    /// Animates the sheet to the given detent (index into
    /// <see cref="ScaffoldBottomSheetOptions.Detents"/>) — the programmatic counterpart of
    /// dragging. No-op before presentation settles.
    /// </summary>
    public Task SnapToDetentAsync(int detentIndex)
    {
        if (_sheetHeight <= 0 || _detentOffsets.Length == 0)
        {
            return Task.CompletedTask;
        }

        var height = ResolveDetentHeight(Math.Clamp(detentIndex, 0, _options.Detents.Length - 1));

        return this.TranslateTo(0, _sheetHeight - height, _animationDuration, Easing.CubicOut);
    }

    /// <summary>The configured maximum sheet width (presenters clamp the window width by it).</summary>
    internal double MaxWidth => _options.MaxWidth;

    /// <summary>Applies the bottom system inset as content padding BEFORE the natural-height measure.</summary>
    internal void PrepareForMeasure(double bottomInset)
        => Padding = new Thickness(0, 0, 0, bottomInset);

    /// <summary>
    /// Computes the sheet geometry from the measured natural height: the sheet is as tall as
    /// its largest detent; smaller detents are reached by translation (0 = fully open).
    /// Returns the sheet height for the presenter to frame, bottom-anchored.
    /// </summary>
    internal double InitializeGeometry(double availableHeight, double naturalHeight)
    {
        _availableHeight = availableHeight;
        _naturalHeight = naturalHeight;

        var detents = _options.Detents is { Length: > 0 } configured ? configured : [ScaffoldSheetDetent.Content];

        var heights = detents
                      .Select(detent => detent.Resolve(availableHeight, naturalHeight))
                      .Distinct()
                      .Order()
                      .ToArray();

        _sheetHeight = heights[^1];

        // TranslationY per detent, ascending ([0] = largest detent = fully open).
        _detentOffsets = [.. heights.Reverse().Select(height => _sheetHeight - height)];

        InitialOffset = _sheetHeight - ResolveDetentHeight(Math.Clamp(_options.InitialDetent, 0, detents.Length - 1));

        return _sheetHeight;
    }

    private double _availableHeight;
    private double _naturalHeight;

    private double ResolveDetentHeight(int detentIndex)
        => _options.Detents[detentIndex].Resolve(_availableHeight, _naturalHeight);

    /// <summary>The translation of the initial detent.</summary>
    internal double InitialOffset { get; private set; }

    /// <summary>Wired by the scaffold to the overlay-entry close path (scrim fade + cleanup included).</summary>
    internal void SetDismissCallback(Func<Task> dismissAsync) => _dismissAsync = dismissAsync;

    /// <summary>Slides in from offscreen to the initial detent (the sheet owns ALL its translation).</summary>
    internal Task EnterAsync()
    {
        TranslationY = _sheetHeight;

        return this.TranslateTo(0, InitialOffset, _animationDuration, Easing.CubicOut);
    }

    /// <summary>Slides out from the current position past the bottom edge.</summary>
    internal Task ExitAsync() => this.TranslateTo(0, _sheetHeight, _animationDuration, Easing.CubicIn);

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panning = true;
                _panStartTranslationY = TranslationY;

                break;

            case GestureStatus.Running when _panning:
                // Between fully open (0 — no over-drag past the largest detent: expansion only
                // exists while a bigger detent does) and fully dismissed (_sheetHeight).
                TranslationY = Math.Clamp(_panStartTranslationY + e.TotalY, 0, _sheetHeight);

                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _panning = false;
                _ = SettleAsync();

                break;
        }
    }

    /// <summary>Snaps to the nearest detent — or dismisses when pulled far enough below the smallest one.</summary>
    private async Task SettleAsync()
    {
        var position = TranslationY;
        var smallestDetentOffset = _detentOffsets[^1];

        if (_options.AllowPullDownToClose
            && position > smallestDetentOffset + _dismissThreshold
            && _dismissAsync is { } dismissAsync)
        {
            await dismissAsync();

            return;
        }

        var target = _detentOffsets.MinBy(offset => Math.Abs(offset - position));
        await this.TranslateTo(0, target, _animationDuration, Easing.CubicOut);
    }
}
