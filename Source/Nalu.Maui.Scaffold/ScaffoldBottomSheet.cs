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

    /// <summary>A detent at the given fraction (0..1) of the available height (window minus the top system inset).</summary>
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
/// top overlay layer and slides from the bottom edge over any chrome. Drag is recognized
/// natively (cooperatively with inner scrollables); detent geometry, snapping and
/// pull-down-to-close live at the virtual view layer — no native sheet controller is involved.
/// </summary>
public sealed class ScaffoldBottomSheetOptions
{
    /// <summary>Gets or sets the scrim brush behind the sheet (gradients supported). Defaults to a theme-aware translucent black.</summary>
    public Brush? Scrim { get; init; }

    /// <summary>Gets or sets whether tapping the scrim closes the sheet. Defaults to true.</summary>
    public bool? CloseOnScrimTap { get; init; }

    /// <summary>
    /// Gets or sets whether the Android system back closes the sheet (iOS has no system back).
    /// Defaults to true; when false, back is consumed without closing while the sheet is topmost.
    /// </summary>
    public bool? CloseOnBack { get; init; }

    /// <summary>
    /// Gets or sets the maximum sheet width. Defaults to unbounded (full window width); when
    /// the window is wider (tablets, landscape), the sheet floats centered at this width,
    /// still bottom-anchored.
    /// </summary>
    public double? MaxWidth { get; init; }

    /// <summary>
    /// Gets or sets the resting heights (order and duplicates are irrelevant — heights are
    /// sorted and de-duplicated). Must be non-empty when set. Defaults to a single
    /// <see cref="ScaffoldSheetDetent.Content"/> detent.
    /// </summary>
    public ScaffoldSheetDetent[]? Detents { get; init; }

    /// <summary>Gets or sets the index (into <see cref="Detents"/>) the sheet opens at. Defaults to 0.</summary>
    public int? InitialDetent { get; init; }

    /// <summary>Gets or sets whether releasing a drag well below the smallest detent (~56dp) dismisses the sheet. Defaults to true.</summary>
    public bool? AllowPullDownToClose { get; init; }

    /// <summary>Gets or sets whether the built-in drag handle is shown. Defaults to true.</summary>
    public bool? ShowDragHandle { get; init; }
}

/// <summary>
/// Attached presentation properties declared on a sheet CONTENT view — the view states how it
/// prefers to be presented, right where it is defined (XAML-friendly, styleable). Call-site
/// <see cref="ScaffoldBottomSheetOptions"/> override per property: a set option wins over the
/// attached value, which wins over the built-in default.
/// </summary>
public static class ScaffoldBottomSheet
{
    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.Detents"/>.</summary>
    public static readonly BindableProperty DetentsProperty =
        BindableProperty.CreateAttached("Detents", typeof(ScaffoldSheetDetent[]), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.InitialDetent"/>.</summary>
    public static readonly BindableProperty InitialDetentProperty =
        BindableProperty.CreateAttached("InitialDetent", typeof(int?), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.AllowPullDownToClose"/>.</summary>
    public static readonly BindableProperty AllowPullDownToCloseProperty =
        BindableProperty.CreateAttached("AllowPullDownToClose", typeof(bool?), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.ShowDragHandle"/>.</summary>
    public static readonly BindableProperty ShowDragHandleProperty =
        BindableProperty.CreateAttached("ShowDragHandle", typeof(bool?), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.MaxWidth"/>.</summary>
    public static readonly BindableProperty MaxWidthProperty =
        BindableProperty.CreateAttached("MaxWidth", typeof(double?), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.Scrim"/>.</summary>
    public static readonly BindableProperty ScrimProperty =
        BindableProperty.CreateAttached("Scrim", typeof(Brush), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.CloseOnScrimTap"/>.</summary>
    public static readonly BindableProperty CloseOnScrimTapProperty =
        BindableProperty.CreateAttached("CloseOnScrimTap", typeof(bool?), typeof(ScaffoldBottomSheet), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldBottomSheetOptions.CloseOnBack"/>.</summary>
    public static readonly BindableProperty CloseOnBackProperty =
        BindableProperty.CreateAttached("CloseOnBack", typeof(bool?), typeof(ScaffoldBottomSheet), null);

    /// <summary>Gets the attached detents.</summary>
    public static ScaffoldSheetDetent[]? GetDetents(BindableObject view) => (ScaffoldSheetDetent[]?)view.GetValue(DetentsProperty);

    /// <summary>Sets the attached detents.</summary>
    public static void SetDetents(BindableObject view, ScaffoldSheetDetent[]? value) => view.SetValue(DetentsProperty, value);

    /// <summary>Gets the attached initial detent index.</summary>
    public static int? GetInitialDetent(BindableObject view) => (int?)view.GetValue(InitialDetentProperty);

    /// <summary>Sets the attached initial detent index.</summary>
    public static void SetInitialDetent(BindableObject view, int? value) => view.SetValue(InitialDetentProperty, value);

    /// <summary>Gets the attached pull-down policy.</summary>
    public static bool? GetAllowPullDownToClose(BindableObject view) => (bool?)view.GetValue(AllowPullDownToCloseProperty);

    /// <summary>Sets the attached pull-down policy.</summary>
    public static void SetAllowPullDownToClose(BindableObject view, bool? value) => view.SetValue(AllowPullDownToCloseProperty, value);

    /// <summary>Gets the attached drag-handle visibility.</summary>
    public static bool? GetShowDragHandle(BindableObject view) => (bool?)view.GetValue(ShowDragHandleProperty);

    /// <summary>Sets the attached drag-handle visibility.</summary>
    public static void SetShowDragHandle(BindableObject view, bool? value) => view.SetValue(ShowDragHandleProperty, value);

    /// <summary>Gets the attached maximum sheet width.</summary>
    public static double? GetMaxWidth(BindableObject view) => (double?)view.GetValue(MaxWidthProperty);

    /// <summary>Sets the attached maximum sheet width.</summary>
    public static void SetMaxWidth(BindableObject view, double? value) => view.SetValue(MaxWidthProperty, value);

    /// <summary>Gets the attached scrim brush.</summary>
    public static Brush? GetScrim(BindableObject view) => (Brush?)view.GetValue(ScrimProperty);

    /// <summary>Sets the attached scrim brush.</summary>
    public static void SetScrim(BindableObject view, Brush? value) => view.SetValue(ScrimProperty, value);

    /// <summary>Gets the attached scrim-tap dismissal policy.</summary>
    public static bool? GetCloseOnScrimTap(BindableObject view) => (bool?)view.GetValue(CloseOnScrimTapProperty);

    /// <summary>Sets the attached scrim-tap dismissal policy.</summary>
    public static void SetCloseOnScrimTap(BindableObject view, bool? value) => view.SetValue(CloseOnScrimTapProperty, value);

    /// <summary>Gets the attached back dismissal policy.</summary>
    public static bool? GetCloseOnBack(BindableObject view) => (bool?)view.GetValue(CloseOnBackProperty);

    /// <summary>Sets the attached back dismissal policy.</summary>
    public static void SetCloseOnBack(BindableObject view, bool? value) => view.SetValue(CloseOnBackProperty, value);
}

/// <summary>The RESOLVED sheet presentation (call-site options ?? attached values ?? defaults).</summary>
internal sealed record ScaffoldSheetPresentation(
    ScaffoldSheetDetent[] Detents,
    int InitialDetent,
    bool AllowPullDownToClose,
    bool ShowDragHandle,
    double MaxWidth
);

/// <summary>
/// The bottom sheet chrome of <see cref="Scaffold.ShowBottomSheetAsync"/>: a top-rounded surface
/// with an optional drag handle hosting the user content. Drag is recognized natively; detent
/// snapping and pull-down-to-close live at the virtual view layer.
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
/// Instances are created per presentation — the type is public as a styling surface;
/// <see cref="SnapToDetentAsync"/> is reachable from the content via
/// <c>(ScaffoldBottomSheetView?)content.Parent?.Parent</c>:
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

    private readonly ScaffoldSheetPresentation _presentation;
    private readonly RoundRectangle _handle;

    private readonly View _contentView;
    private double[] _detentOffsets = [0];
    private double _sheetHeight;
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
    /// Gets or sets the sheet surface brush. Defaults to opaque white — set it per theme.
    /// Drives the view's own <see cref="VisualElement.Background"/> — style THIS, not
    /// <c>Background</c>.
    /// </summary>
    public Brush? SheetBackground
    {
        get => (Brush?)GetValue(SheetBackgroundProperty);
        set => SetValue(SheetBackgroundProperty, value);
    }

    /// <summary>Gets or sets the radius of the sheet's top corners. Defaults to 16.</summary>
    public double SheetCornerRadius
    {
        get => (double)GetValue(SheetCornerRadiusProperty);
        set => SetValue(SheetCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the drag handle color. Defaults to #4D8E8E93.</summary>
    public Color HandleColor
    {
        get => (Color)GetValue(HandleColorProperty);
        set => SetValue(HandleColorProperty, value);
    }

    internal ScaffoldBottomSheetView(View content, ScaffoldSheetPresentation presentation)
    {
        _presentation = presentation;
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
            IsVisible = presentation.ShowDragHandle
        };

        var layout = new Grid
        {
            // Same trap as the tab bar (see ScaffoldTabBarView): a MAUI Grid self-pads from the
            // root window insets on Android before it knows its real window position — an
            // overlay-mounted grid gets offset by the status bar. Insets are the presenter's job.
            SafeAreaEdges = SafeAreaEdges.None,
            RowDefinitions =
            {
                new RowDefinition(presentation.ShowDragHandle ? new GridLength(20) : new GridLength(0)),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(_handle, 0, 0);
        layout.Add(content, 0, 1);
        _contentView = content;

        Content = layout;

        // NO MAUI pan here: it steals moves from inner scrollables on BOTH platforms. The
        // drag is platform-owned — iOS attaches a cooperative native recognizer at mount
        // (ScaffoldBottomSheetGesture), Android hosts the sheet in a nested-scroll parent
        // with a raw-touch fallback for non-scrollable surfaces (ScaffoldBottomSheetNestedHost).

        // Defaults never raise propertyChanged: seed once from the current values.
        Background = SheetBackground;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCornerRadius, SheetCornerRadius, 0, 0) };
        _handle.Fill = new SolidColorBrush(HandleColor);
    }

    /// <summary>
    /// Animates the sheet to the given detent (index into
    /// <see cref="ScaffoldBottomSheetOptions.Detents"/>, clamped into range) — the programmatic
    /// counterpart of dragging. No-op until the sheet's geometry is initialized.
    /// </summary>
    public Task SnapToDetentAsync(int detentIndex)
    {
        if (_sheetHeight <= 0 || _detentOffsets.Length == 0)
        {
            return Task.CompletedTask;
        }

        var height = ResolveDetentHeight(Math.Clamp(detentIndex, 0, _presentation.Detents.Length - 1));

        return this.TranslateToAsync(0, _sheetHeight - height, _animationDuration, Easing.CubicOut);
    }

    /// <summary>The configured maximum sheet width (presenters clamp the window width by it).</summary>
    internal double MaxWidth => _presentation.MaxWidth;

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

        var detents = _presentation.Detents is { Length: > 0 } configured ? configured : [ScaffoldSheetDetent.Content];

        var heights = detents
                      .Select(detent => detent.Resolve(availableHeight, naturalHeight))
                      .Distinct()
                      .Order()
                      .ToArray();

        _sheetHeight = heights[^1];

        // TranslationY per detent, ascending ([0] = largest detent = fully open).
        _detentOffsets = [.. heights.Reverse().Select(height => _sheetHeight - height)];

        InitialOffset = _sheetHeight - ResolveDetentHeight(Math.Clamp(_presentation.InitialDetent, 0, detents.Length - 1));

        // The content row measures UNBOUNDED (Auto — required for Content-detent natural
        // sizing), so scrollable content would inflate to its full content height and lose
        // its scroll range. Clamp it to the space the sheet actually gives it.
        var handleRowHeight = _presentation.ShowDragHandle ? 20 : 0;
        _contentView.MaximumHeightRequest = Math.Max(0, _sheetHeight - handleRowHeight - Padding.VerticalThickness);

        return _sheetHeight;
    }

    private double _availableHeight;
    private double _naturalHeight;

    private double ResolveDetentHeight(int detentIndex)
        => _presentation.Detents[detentIndex].Resolve(_availableHeight, _naturalHeight);

    /// <summary>The translation of the initial detent.</summary>
    internal double InitialOffset { get; private set; }

    /// <summary>The full sheet height (translation at full dismissal); 0 before geometry init.</summary>
    internal double SheetHeight => _sheetHeight;

    /// <summary>Whether the sheet rests at its tallest detent (content may scroll freely).</summary>
    internal bool IsFullyOpen => TranslationY <= 0.5;

    /// <summary>
    /// Platform gesture controllers drive the drag through these: <see cref="DragBy"/> moves
    /// the sheet by a delta in device-independent units (clamped to the valid range) and
    /// returns the amount actually consumed; <see cref="SettleFromGestureAsync"/> snaps to
    /// the nearest detent or dismisses, exactly like a MAUI pan release.
    /// </summary>
    internal double DragBy(double deltaY)
    {
        var previous = TranslationY;
        TranslationY = Math.Clamp(previous + deltaY, 0, _sheetHeight);

        return TranslationY - previous;
    }

    /// <inheritdoc cref="DragBy"/>
    internal Task SettleFromGestureAsync() => SettleAsync();

    /// <summary>Wired by the scaffold to the overlay-entry close path (scrim fade + cleanup included).</summary>
    internal void SetDismissCallback(Func<Task> dismissAsync) => _dismissAsync = dismissAsync;

    /// <summary>Slides in from offscreen to the initial detent (the sheet owns ALL its translation).</summary>
    internal Task EnterAsync()
    {
        TranslationY = _sheetHeight;

        return this.TranslateToAsync(0, InitialOffset, _animationDuration, Easing.CubicOut);
    }

    /// <summary>Slides out from the current position past the bottom edge.</summary>
    internal Task ExitAsync() => this.TranslateToAsync(0, _sheetHeight, _animationDuration, Easing.CubicIn);


    /// <summary>Snaps to the nearest detent — or dismisses when pulled far enough below the smallest one.</summary>
    private async Task SettleAsync()
    {
        var position = TranslationY;
        var smallestDetentOffset = _detentOffsets[^1];

        if (_presentation.AllowPullDownToClose
            && position > smallestDetentOffset + _dismissThreshold
            && _dismissAsync is { } dismissAsync)
        {
            await dismissAsync();

            return;
        }

        var target = _detentOffsets.MinBy(offset => Math.Abs(offset - position));
        await this.TranslateToAsync(0, target, _animationDuration, Easing.CubicOut);
    }
}
