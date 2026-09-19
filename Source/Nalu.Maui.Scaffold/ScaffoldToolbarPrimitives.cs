using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// Observes a page's toolbar items through the inherited <see cref="ScaffoldNavBarContext"/>:
/// the collection swap, its mutations, and every item's <see cref="ToolbarItem.Order"/> /
/// <see cref="ToolbarItem.Priority"/> — everything that changes WHICH item goes WHERE. An item's
/// text, icon and enabled state are the business of the button that renders it.
/// </summary>
internal sealed class ScaffoldToolbarItemsObserver
{
    private readonly Action _changed;
    private readonly List<ToolbarItem> _observedItems = [];
    private ScaffoldNavBarContext? _context;
    private INotifyCollectionChanged? _collection;

    public ScaffoldToolbarItemsObserver(Action changed) => _changed = changed;

    /// <summary>The observed collection — the page's own, live.</summary>
    public IList<ToolbarItem>? Items => _context?.ToolbarItems;

    /// <summary>The items shown as buttons (<see cref="ToolbarItemOrder.Default"/> counts as primary), by ascending priority; declaration order breaks ties.</summary>
    public static IEnumerable<ToolbarItem> PrimaryOf(IList<ToolbarItem>? items)
        => (items ?? []).Where(static item => item.Order != ToolbarItemOrder.Secondary).OrderBy(static item => item.Priority);

    /// <summary>The items folded into the overflow menu, by ascending priority; declaration order breaks ties.</summary>
    public static IEnumerable<ToolbarItem> SecondaryOf(IList<ToolbarItem>? items)
        => (items ?? []).Where(static item => item.Order == ToolbarItemOrder.Secondary).OrderBy(static item => item.Priority);

    /// <summary>Retargets the observation (null detaches); the change callback runs once either way.</summary>
    public void SetContext(ScaffoldNavBarContext? context)
    {
        if (ReferenceEquals(_context, context))
        {
            return;
        }

        if (_context is not null)
        {
            _context.PropertyChanged -= OnContextPropertyChanged;
        }

        _context = context;

        if (context is not null)
        {
            context.PropertyChanged += OnContextPropertyChanged;
        }

        Resync();
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // An empty name is "everything changed" (the scaffold-level forwarder swap).
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ScaffoldNavBarContext.ToolbarItems))
        {
            Resync();
        }
    }

    /// <summary>Re-reads the whole collection — a handful of items, so a full pass on every change is the simplest correct thing.</summary>
    private void Resync()
    {
        Unobserve();

        if (Items is { } items)
        {
            _collection = items as INotifyCollectionChanged;

            if (_collection is not null)
            {
                _collection.CollectionChanged += OnCollectionChanged;
            }

            foreach (var item in items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                _observedItems.Add(item);
            }
        }

        _changed();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Resync();

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ToolbarItem.Order) or nameof(ToolbarItem.Priority))
        {
            _changed();
        }
    }

    private void Unobserve()
    {
        if (_collection is not null)
        {
            _collection.CollectionChanged -= OnCollectionChanged;
            _collection = null;
        }

        foreach (var item in _observedItems)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        _observedItems.Clear();
    }
}

/// <summary>
/// The nav bar's rendering of the page's <see cref="Page.ToolbarItems"/>: primary items
/// (<see cref="ToolbarItemOrder.Primary"/> and <see cref="ToolbarItemOrder.Default"/>) as
/// <see cref="ScaffoldToolbarItemButton"/>s in ascending <see cref="ToolbarItem.Priority"/> —
/// as many as FIT the width the bar offers; the rest fold into the
/// <see cref="ScaffoldToolbarOverflowButton"/>'s menu ahead of the
/// <see cref="ToolbarItemOrder.Secondary"/> items (Android's "if room" semantics; MAUI's native
/// bars squeeze the title instead). Follows the collection live — items added, removed,
/// reordered or moved between surfaces at runtime are reflected at once. Drop it anywhere
/// inside a custom nav bar — it binds to the inherited <see cref="ScaffoldNavBarContext"/>
/// and fits whatever width its host measures it with.
/// </summary>
public sealed class ScaffoldToolbarItemsView : Layout
{
    private readonly List<ScaffoldToolbarItemButton> _buttons = [];
    private readonly ScaffoldToolbarItemsObserver _observer;
    private readonly ScaffoldToolbarOverflowButton _overflowButton;

    /// <summary>Initializes the strip.</summary>
    public ScaffoldToolbarItemsView()
    {
        VerticalOptions = LayoutOptions.Center;

        // The overflow button stays LAST in child order; the strip decides when it shows.
        _overflowButton = new ScaffoldToolbarOverflowButton { AutomationId = "NavBarToolbarOverflowButton", IsManagedByStrip = true };
        Add(_overflowButton);

        _observer = new ScaffoldToolbarItemsObserver(Rebuild);
    }

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        _observer?.SetContext(BindingContext as ScaffoldNavBarContext);
    }

    /// <summary>The primary buttons, in priority order (test seam).</summary>
    internal IReadOnlyList<ScaffoldToolbarItemButton> Buttons => _buttons;

    /// <summary>The overflow button this strip manages (test seam).</summary>
    internal ScaffoldToolbarOverflowButton OverflowButton => _overflowButton;

    /// <summary>Whether the page has secondary items — the menu exists regardless of fit.</summary>
    internal bool HasSecondaryItems => ScaffoldToolbarItemsObserver.SecondaryOf(_observer.Items).Any();

    /// <summary>
    /// The fit: buttons are taken in priority order while they fit; the first one that doesn't
    /// stops the intake (a later, narrower item never jumps ahead of a wider one — order is a
    /// promise). The overflow button is part of the picture as soon as anything must fold, or
    /// secondary items exist.
    /// </summary>
    /// <returns>How many leading buttons fit, whether the overflow button shows, and the resulting width.</returns>
    internal static (int FittingCount, bool ShowsOverflow, double Width) Fit(IReadOnlyList<double> widths, double overflowWidth, bool hasSecondary, double available)
    {
        var total = widths.Sum();

        if (!hasSecondary && total <= available)
        {
            return (widths.Count, false, total);
        }

        var budget = available - overflowWidth;
        var width = 0.0;
        var count = 0;

        foreach (var itemWidth in widths)
        {
            if (width + itemWidth > budget)
            {
                break;
            }

            width += itemWidth;
            count++;
        }

        return (count, true, width + overflowWidth);
    }

    private void Rebuild()
    {
        foreach (var button in _buttons)
        {
            Remove(button);
            button.Item = null;
        }

        _buttons.Clear();

        var index = 0;

        foreach (var item in ScaffoldToolbarItemsObserver.PrimaryOf(_observer.Items))
        {
            var button = new ScaffoldToolbarItemButton { Item = item };
            _buttons.Add(button);

            // Before the overflow button, which stays last.
            Insert(index++, button);
        }
    }

    private sealed class Manager(ScaffoldToolbarItemsView strip) : ILayoutManager
    {
        private int _fittingCount;
        private bool _showsOverflow;

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var buttons = strip._buttons;
            var widths = new double[buttons.Count];
            var height = 0.0;

            // Every button is measured, hidden ones included: what fits is decided from the
            // natural widths, never from the previous plan, so the plan cannot feed itself.
            for (var i = 0; i < buttons.Count; i++)
            {
                var size = ((IView)buttons[i]).Measure(double.PositiveInfinity, heightConstraint);
                widths[i] = size.Width;
                height = Math.Max(height, size.Height);
            }

            var overflow = ((IView)strip._overflowButton).Measure(double.PositiveInfinity, heightConstraint);
            var (count, showsOverflow, width) = Fit(widths, overflow.Width, strip.HasSecondaryItems, widthConstraint);
            _fittingCount = count;
            _showsOverflow = showsOverflow;

            if (showsOverflow)
            {
                height = Math.Max(height, overflow.Height);
            }

            // Out-of-plan buttons are HIDDEN, not merely parked: parked offscreen they would stay
            // in the accessibility tree, announced but unreachable (the tab bar's lesson).
            for (var i = 0; i < buttons.Count; i++)
            {
                var inPlan = i < count;

                if (buttons[i].IsVisible != inPlan)
                {
                    buttons[i].IsVisible = inPlan;
                }
            }

            if (strip._overflowButton.IsVisible != showsOverflow)
            {
                strip._overflowButton.IsVisible = showsOverflow;
            }

            // What folded is what the menu lists first; a menu open on a stale plan closes.
            var squeezed = buttons.Skip(count).Select(static button => button.Item!).ToList();

            if (!squeezed.SequenceEqual(strip._overflowButton.SqueezedItems))
            {
                strip._overflowButton.SqueezedItems = squeezed;

                if (strip._overflowButton.IsOpen)
                {
                    strip.Dispatcher.Dispatch(() => _ = strip._overflowButton.CloseMenuAsync());
                }
            }

            return new Size(width, height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var rtl = ((IView)strip).FlowDirection == FlowDirection.RightToLeft;
            var buttons = strip._buttons;
            var x = 0.0;

            for (var i = 0; i < buttons.Count; i++)
            {
                if (i < _fittingCount)
                {
                    x = Place(buttons[i], x);
                }
                else
                {
                    Park(buttons[i]);
                }
            }

            if (_showsOverflow)
            {
                Place(strip._overflowButton, x);
            }
            else
            {
                Park(strip._overflowButton);
            }

            return bounds.Size;

            double Place(View view, double offset)
            {
                var width = view.DesiredSize.Width;
                ((IView)view).Arrange(new Rect(rtl ? bounds.Left + bounds.Width - offset - width : bounds.Left + offset, bounds.Top, width, bounds.Height));

                return offset + width;
            }

            // A zero-size arrange leaves the platform view at its stale frame on iOS.
            static void Park(View view)
                => ((IView)view).Arrange(new Rect(-10000, -10000, view.DesiredSize.Width, view.DesiredSize.Height));
        }
    }
}

/// <summary>
/// One primary toolbar item in the nav bar: its icon (24dp, centered in a 44dp tap target) when
/// <see cref="MenuItem.IconImageSource"/> is set, its <see cref="MenuItem.Text"/> otherwise —
/// the native toolbar convention, with the text always serving as the accessibility description.
/// A tap activates the item exactly as a native toolbar would (its <see cref="MenuItem.Command"/>
/// and <see cref="MenuItem.Clicked"/>); a disabled item is dimmed and inert. Follows the item's
/// text, icon and enabled state live.
/// </summary>
/// <remarks>
/// Colors: an explicitly set (or styled) <see cref="TextColor"/> wins, then the effective
/// <see cref="ScaffoldNavBarContext.Foreground"/>, then the built-in default. It colors the
/// text — and a <see cref="FontImageSource"/> icon declared WITHOUT a color, which is tinted
/// like a native template glyph; any other icon renders exactly as given. Style it with
/// <c>&lt;Style TargetType="nalu:ScaffoldToolbarItemButton"&gt;</c>.
/// </remarks>
public sealed class ScaffoldToolbarItemButton : Border
{
    private readonly Label _label;
    private readonly Image _icon;
    private readonly RoundRectangle _pressHighlight;
    private readonly TapGestureRecognizer _tap = new();
    private ScaffoldNavBarContext? _observedContext;
    private ToolbarItem? _observedItem;

    // Callback caveat (applies to EVERY styling property here): implicit styles are applied by
    // the VisualElement BASE ctor (MergedStyle), before this class's ctor body has built its
    // subviews — callbacks must tolerate null fields; the ctor seeds the final values.

    /// <summary>Bindable property for <see cref="Item"/>.</summary>
    public static readonly BindableProperty ItemProperty =
        GenericBindableProperty<ScaffoldToolbarItemButton>.Create<ToolbarItem?>(
            nameof(Item),
            propertyChanged: static button => (oldValue, value) => button.ObserveItem(oldValue, value)
        );

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldToolbarItemButton>.Create(
            nameof(TextColor),
            ScaffoldNavBarDefaults.Foreground,
            propertyChanged: static button => (_, _) => button.ApplyEffectiveColors()
        );

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldToolbarItemButton>.Create<string?>(
            nameof(FontFamily),
            propertyChanged: static button => (_, value) => button._label?.FontFamily = value
        );

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldToolbarItemButton>.Create(
            nameof(FontSize),
            16.0,
            propertyChanged: static button => (_, value) => button._label?.FontSize = value
        );

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty =
        GenericBindableProperty<ScaffoldToolbarItemButton>.Create(
            nameof(FontAttributes),
            FontAttributes.None,
            propertyChanged: static button => (_, value) => button._label?.FontAttributes = value
        );

    /// <summary>Bindable property for <see cref="PressedBrush"/>.</summary>
    public static readonly BindableProperty PressedBrushProperty =
        GenericBindableProperty<ScaffoldToolbarItemButton>.Create<Brush?>(
            nameof(PressedBrush),
            propertyChanged: static button => (_, _) => button.ApplyEffectiveColors()
        );

    /// <summary>Gets or sets the toolbar item this button renders and activates.</summary>
    public ToolbarItem? Item
    {
        get => (ToolbarItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    /// <summary>
    /// Gets or sets the text (and untinted-font-icon) color. When not set (directly or via
    /// style), the effective <see cref="ScaffoldNavBarContext.Foreground"/> applies.
    /// </summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the text font family.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the text font size.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the text font attributes.</summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>
    /// Gets or sets the press-feedback brush (a pill-shaped pulse filling the tap target).
    /// When not set, a translucent tint of the effective text color is used.
    /// </summary>
    public Brush? PressedBrush
    {
        get => (Brush?)GetValue(PressedBrushProperty);
        set => SetValue(PressedBrushProperty, value);
    }

    /// <summary>Initializes the button.</summary>
    public ScaffoldToolbarItemButton()
    {
        StrokeThickness = 0;
        Background = null;
        HeightRequest = 44;
        MinimumWidthRequest = 44;
        VerticalOptions = LayoutOptions.Center;

        // The pulse sits BELOW the content, filling the tap target; InputTransparent keeps it
        // out of every hit-test path.
        _pressHighlight = new RoundRectangle
        {
            CornerRadius = new CornerRadius(22),
            Opacity = 0,
            InputTransparent = true
        };

        // 24dp glyph + 10dp either side = the same 44dp footprint as the drawn-glyph buttons.
        _icon = new Image
        {
            Aspect = Aspect.AspectFit,
            WidthRequest = 24,
            HeightRequest = 24,
            Margin = new Thickness(10, 0),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        };

        _label = new Label
        {
            Margin = new Thickness(12, 0),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };

        var touchSurface = new Grid { Children = { _pressHighlight, _icon, _label } };
        Content = touchSurface;

        _tap.Tapped += (_, _) => Activate();
        GestureRecognizers.Add(_tap);
        ScaffoldPressable.Observe(touchSurface, OnPressedPulse);

        // Defaults never raise propertyChanged: seed once from the current values.
        _label.FontFamily = FontFamily;
        _label.FontSize = FontSize;
        _label.FontAttributes = FontAttributes;
        ApplyEffectiveColors();
        ObserveItem(null, Item);
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_observedContext is not null)
        {
            _observedContext.PropertyChanged -= OnContextPropertyChanged;
            _observedContext = null;
        }

        if (BindingContext is ScaffoldNavBarContext context)
        {
            _observedContext = context;
            context.PropertyChanged += OnContextPropertyChanged;
        }

        ApplyEffectiveColors();
    }

    /// <summary>
    /// Activates the item the way a native toolbar does — command and <see cref="MenuItem.Clicked"/>
    /// — unless it is disabled: MAUI's own activation fires <c>Clicked</c> regardless, because a
    /// native disabled item can never be tapped in the first place; here the gate is ours.
    /// </summary>
    internal void Activate()
    {
        if (Item is { IsEnabled: true } item)
        {
            ((IMenuItemController)item).Activate();
        }
    }

    private void ObserveItem(ToolbarItem? previous, ToolbarItem? item)
    {
        if (previous is not null && ReferenceEquals(previous, _observedItem))
        {
            previous.PropertyChanged -= OnItemPropertyChanged;
        }

        _observedItem = item;

        if (item is not null)
        {
            item.PropertyChanged += OnItemPropertyChanged;

            // The item's own AutomationId identifies the button, as it identifies the native
            // control — MAUI allows setting it once, so the first item wins.
            if (AutomationId is null && item.AutomationId is { } automationId)
            {
                AutomationId = automationId;
            }
        }

        ApplyItem();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MenuItem.Text) or nameof(MenuItem.IconImageSource) or nameof(MenuItem.IsEnabled))
        {
            ApplyItem();
        }
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaffoldNavBarContext.Foreground))
        {
            ApplyEffectiveColors();
        }
    }

    private Color EffectiveTextColor
        => IsSet(TextColorProperty)
            ? TextColor
            : _observedContext?.Foreground ?? ScaffoldNavBarDefaults.Foreground;

    /// <summary>Text, icon and enabled state — the item's own, applied whole.</summary>
    private void ApplyItem()
    {
        if (_label is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the subviews.
            return;
        }

        var item = Item;
        var icon = item?.IconImageSource;
        var enabled = item?.IsEnabled ?? true;

        _label.Text = item?.Text;
        _icon.Source = ScaffoldNavBarDefaults.TintFontIcon(icon, EffectiveTextColor);
        _icon.IsVisible = icon is not null;
        _label.IsVisible = icon is null;

        // Text is always the accessible name — an icon-only button would otherwise be mute.
        SemanticProperties.SetDescription(this, item?.Text);

        IsEnabled = enabled;
        Opacity = enabled ? 1 : 0.38;
    }

    /// <summary>
    /// The effective color: an explicitly set (or styled) <see cref="TextColor"/> wins, then the
    /// appearance-driven context foreground, then the built-in default. Read-path only —
    /// nothing ever writes into <see cref="TextColorProperty"/>, so styles keep working.
    /// </summary>
    private void ApplyEffectiveColors()
    {
        if (_label is null)
        {
            return;
        }

        var color = EffectiveTextColor;
        _label.TextColor = color;
        _pressHighlight.Fill = PressedBrush ?? new SolidColorBrush(color.WithAlpha(0.14f));

        // A tinted font icon carries the color inside the source: re-derive it.
        _icon.Source = ScaffoldNavBarDefaults.TintFontIcon(Item?.IconImageSource, color);
    }

    private void OnPressedPulse()
    {
        Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(_pressHighlight);
        _pressHighlight.Opacity = 1;
        _ = _pressHighlight.FadeToAsync(0, 400, Easing.CubicOut);
    }
}

/// <summary>
/// The nav bar overflow (⋮) button: visible while the page has
/// <see cref="ToolbarItemOrder.Secondary"/> toolbar items — or, inside a
/// <see cref="ScaffoldToolbarItemsView"/>, while primary items had to fold — it presents them
/// in a <see cref="ScaffoldToolbarOverflowView"/> anchored below itself (a second tap, the scrim or
/// the system back dismiss it; a navigation closes it like any overlay). Drop it anywhere inside
/// a custom nav bar — it binds to the inherited <see cref="ScaffoldNavBarContext"/>. Style it
/// like every glyph button (<see cref="ScaffoldNavBarButtonBase"/>).
/// </summary>
public sealed class ScaffoldToolbarOverflowButton : ScaffoldNavBarButtonBase
{
    // Three 4dp dots on the 24-box's vertical axis, painted as a fill.
    private const string _dotsGlyph = "M12 4 A2 2 0 1 1 12 8 A2 2 0 1 1 12 4 Z M12 10 A2 2 0 1 1 12 14 A2 2 0 1 1 12 10 Z M12 16 A2 2 0 1 1 12 20 A2 2 0 1 1 12 16 Z";

    private readonly ScaffoldToolbarItemsObserver _observer;

    // The PRESENTATION, not the handle: a row tapped while the open animation is still running
    // must close the menu it belongs to, and the handle only exists once presenting returns.
    private Task<IScaffoldPopup>? _presenting;

    /// <summary>
    /// True inside a <see cref="ScaffoldToolbarItemsView"/>, which then owns this button's
    /// visibility (it shows for folded primary items too, and only once the strip has measured);
    /// standalone, the button shows itself while secondary items exist.
    /// </summary>
    internal bool IsManagedByStrip { get; init; }

    /// <summary>Primary items the strip could not fit: listed in the menu ahead of the secondary ones.</summary>
    internal IReadOnlyList<ToolbarItem> SqueezedItems { get; set; } = [];

    /// <summary>Initializes the overflow button (built-in ⋮ glyph).</summary>
    public ScaffoldToolbarOverflowButton()
        : base(_dotsGlyph, filledGlyph: true)
    {
        IsVisible = false;
        SetTapHandler(ToggleAsync);
        _observer = new ScaffoldToolbarItemsObserver(OnItemsChanged);
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        _observer?.SetContext(BindingContext as ScaffoldNavBarContext);
    }

    /// <summary>Gets whether the overflow menu is presented (or being presented).</summary>
    public bool IsOpen => _presenting is { } presenting && (!presenting.IsCompleted || presenting is { IsCompletedSuccessfully: true, Result.IsOpen: true });

    /// <summary>Opens the overflow menu, or closes it when it is presented.</summary>
    public Task ToggleAsync() => _presenting is not null ? CloseAsync() : OpenAsync();

    /// <summary>Closes the menu if it is presented (the strip's plan changed under it).</summary>
    internal Task CloseMenuAsync() => CloseAsync();

    private void OnItemsChanged()
    {
        if (!IsManagedByStrip)
        {
            IsVisible = ScaffoldToolbarItemsObserver.SecondaryOf(_observer.Items).Any();
        }

        // What an open menu lists is no longer what the page has: dismiss rather than go stale.
        _ = CloseAsync();
    }

    private async Task OpenAsync()
    {
        // Folded primary items first — they outrank the secondary ones by declaration.
        var items = SqueezedItems.Concat(ScaffoldToolbarItemsObserver.SecondaryOf(_observer.Items)).ToList();

        if (items.Count == 0 || this.GetScaffoldOrDefault() is not { } scaffold)
        {
            return;
        }

        var panel = new ScaffoldToolbarOverflowView(items, CloseAsync);

        // The popup attaches the panel to the scaffold's tree before resolving its scrim, so the
        // panel's styled Scrim (mirrored into the attached property) is honored.
        var presenting = scaffold.ShowPopupAsync(
            panel,
            new ScaffoldPopupOptions
            {
                Anchor = this,
                AnchorOffset = new Point(0, 2),
                Margin = new Thickness(8),
                CustomPlacer = new ScaffoldToolbarOverflowPlacer(((IView)this).FlowDirection == FlowDirection.RightToLeft)
            }
        );

        _presenting = presenting;

        try
        {
            // Our own presentation: started and awaited on the UI thread, never blocked on — the
            // deadlock the analyzer guards against needs a synchronous wait somewhere.
#pragma warning disable VSTHRD003
            var popup = await presenting;
            await popup.Closed;
#pragma warning restore VSTHRD003
        }
        finally
        {
            panel.Cleanup();

            if (ReferenceEquals(_presenting, presenting))
            {
                _presenting = null;
            }
        }
    }

    /// <summary>Closes the menu — the one presented, or the one still being presented.</summary>
    private async Task CloseAsync()
    {
        if (_presenting is { } presenting)
        {
            // Our own presentation: started and awaited on the UI thread, never blocked on — the
            // deadlock the analyzer guards against needs a synchronous wait somewhere.
#pragma warning disable VSTHRD003
            var popup = await presenting;
#pragma warning restore VSTHRD003
            await popup.CloseAsync();
        }
    }
}

/// <summary>
/// The overflow menu's dropdown geometry: the panel hangs BELOW the anchor with its END edge on
/// the anchor's end edge — a trailing button's menu opens inward, toward the title, never off
/// the screen — flips above when it doesn't fit below, and stays inside the safe area.
/// </summary>
internal sealed class ScaffoldToolbarOverflowPlacer(bool isRtl) : IScaffoldPopupPlacer
{
    public Rect Place(Rect area, Size contentSize, Rect? anchorBounds)
    {
        if (anchorBounds is not { } anchor)
        {
            return new Rect(
                area.X + (area.Width - contentSize.Width) / 2,
                area.Y + (area.Height - contentSize.Height) / 2,
                contentSize.Width,
                contentSize.Height
            );
        }

        var x = isRtl ? anchor.Left : anchor.Right - contentSize.Width;
        var y = anchor.Bottom;

        if (y + contentSize.Height > area.Bottom)
        {
            y = anchor.Top - contentSize.Height;
        }

        return new Rect(
            Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - contentSize.Width)),
            Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - contentSize.Height)),
            contentSize.Width,
            contentSize.Height
        );
    }
}

/// <summary>
/// The overflow menu of the default nav bar: a rounded panel listing the page's
/// <see cref="ToolbarItemOrder.Secondary"/> toolbar items as rows (icon when set, then text),
/// by ascending <see cref="ToolbarItem.Priority"/>. A row dismisses the menu first, then
/// activates its item; disabled items are dimmed and inert. Built fresh on every open.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created by <see cref="ScaffoldToolbarOverflowButton"/> — the type is public
/// purely as a styling surface. Unstyled, the panel background and the text follow the app
/// theme (light / dark); a style pins them:
/// <code>
/// &lt;Style TargetType="nalu:ScaffoldToolbarOverflowView"&gt;
///     &lt;Setter Property="PanelBackground" Value="{AppThemeBinding Light=..., Dark=...}" /&gt;
///     &lt;Setter Property="TextColor" Value="{AppThemeBinding Light=..., Dark=...}" /&gt;
/// &lt;/Style&gt;
/// </code>
/// Font icons declared without a color take the effective text color; other icons render as given.
/// </para>
/// <para>
/// The panel hugs its widest row, between <see cref="VisualElement.MinimumWidthRequest"/> (200)
/// and <see cref="VisualElement.MaximumWidthRequest"/> (280) — both plain properties, style them
/// to change the bounds.
/// </para>
/// </remarks>
public sealed class ScaffoldToolbarOverflowView : Border
{
    private static readonly Color _lightPanel = Color.FromArgb("#FAFFFFFF");
    private static readonly Color _darkPanel = Color.FromArgb("#FA2C2C2E");
    private static readonly Color _lightText = ScaffoldNavBarDefaults.Foreground;
    private static readonly Color _darkText = Color.FromArgb("#F2F2F7");

    private readonly List<Row> _rows = [];

    /// <summary>Bindable property for <see cref="PanelBackground"/>.</summary>
    public static readonly BindableProperty PanelBackgroundProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create<Brush?>(
            nameof(PanelBackground),
            propertyChanged: static panel => (_, _) => panel.ApplyPanelBackground()
        );

    /// <summary>Bindable property for <see cref="PanelCornerRadius"/>.</summary>
    public static readonly BindableProperty PanelCornerRadiusProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create(
            nameof(PanelCornerRadius),
            12.0,
            propertyChanged: static panel => (_, value) => panel.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(value) }
        );

    /// <summary>Bindable property for <see cref="PanelShadow"/>.</summary>
    public static readonly BindableProperty PanelShadowProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create<Shadow>(
            nameof(PanelShadow),
            defaultValueCreator: static _ => new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.22f,
                Radius = 18,
                Offset = new Point(0, 4)
            },
            propertyChanged: static panel => (_, value) => panel.Shadow = value
        );

    /// <summary>Bindable property for <see cref="Scrim"/>.</summary>
    public static readonly BindableProperty ScrimProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create<Brush?>(
            nameof(Scrim),
            defaultValueCreator: static _ => new SolidColorBrush(Colors.Transparent),
            propertyChanged: static panel => (_, value) => ScaffoldPopup.SetScrim(panel, value)
        );

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create<Color?>(
            nameof(TextColor),
            propertyChanged: static panel => (_, _) => panel.ApplyRowAppearance()
        );

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create<string?>(
            nameof(FontFamily),
            propertyChanged: static panel => (_, _) => panel.ApplyRowAppearance()
        );

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldToolbarOverflowView>.Create(
            nameof(FontSize),
            16.0,
            propertyChanged: static panel => (_, _) => panel.ApplyRowAppearance()
        );

    /// <summary>
    /// Gets or sets the panel background. Null (the default) follows the app theme — a
    /// near-opaque white in light mode, a near-opaque dark gray in dark mode. Drives the view's
    /// own <see cref="VisualElement.Background"/> — style THIS, not <c>Background</c>.
    /// </summary>
    public Brush? PanelBackground
    {
        get => (Brush?)GetValue(PanelBackgroundProperty);
        set => SetValue(PanelBackgroundProperty, value);
    }

    /// <summary>Gets or sets the panel corner radius.</summary>
    public double PanelCornerRadius
    {
        get => (double)GetValue(PanelCornerRadiusProperty);
        set => SetValue(PanelCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the panel shadow.</summary>
    public Shadow PanelShadow
    {
        get => (Shadow)GetValue(PanelShadowProperty);
        set => SetValue(PanelShadowProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrim brush behind the menu — transparent by default (a dropdown), yet
    /// still blocking interaction with everything below and closing the menu on tap.
    /// </summary>
    public Brush? Scrim
    {
        get => (Brush?)GetValue(ScrimProperty);
        set => SetValue(ScrimProperty, value);
    }

    /// <summary>
    /// Gets or sets the row text (and untinted-font-icon) color. Null (the default) follows the
    /// app theme — near-black in light mode, near-white in dark mode.
    /// </summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the row font family.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the row font size.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The items listed, in row order (test seam).</summary>
    internal IReadOnlyList<ToolbarItem> Items => _rows.Select(static row => row.Item).ToList();

    /// <summary>The text color in effect: the styled one, else the theme's (test seam).</summary>
    internal Color EffectiveTextColor => TextColor ?? (IsDark ? _darkText : _lightText);

    private static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    internal ScaffoldToolbarOverflowView(IReadOnlyList<ToolbarItem> items, Func<Task> closeAsync)
    {
        AutomationId = "NavBarToolbarOverflowPanel";
        StrokeThickness = 0;

        // The panel hugs its content between these bounds (Material's 280 maximum; a 200
        // minimum so short menus don't look like chips). Rows measure to content, so the popup
        // measures the panel at its natural width instead of the whole presentation area.
        MinimumWidthRequest = 200;
        MaximumWidthRequest = 280;
        HorizontalOptions = LayoutOptions.Start;
        Padding = new Thickness(0, 8);

        // An icon slot only when some row has an icon — text-only menus don't indent.
        var hasIcons = items.Any(static item => item.IconImageSource is not null);
        var stack = new VerticalStackLayout { Spacing = 0 };

        foreach (var item in items)
        {
            var row = new Row(item, hasIcons, closeAsync);
            _rows.Add(row);
            stack.Add(row);
        }

        Content = stack;

        // A theme change while the menu is open recolors the unstyled defaults in place.
        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        // Defaults never raise propertyChanged: seed once from the current values.
        ApplyPanelBackground();
        Shadow = PanelShadow;
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(PanelCornerRadius) };
        ScaffoldPopup.SetScrim(this, Scrim);
        ApplyRowAppearance();
    }

    /// <summary>Detaches the rows' item subscriptions and the theme observation; invoked once the popup has closed.</summary>
    internal void Cleanup()
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        foreach (var row in _rows)
        {
            row.Unsubscribe();
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        ApplyPanelBackground();
        ApplyRowAppearance();
    }

    private void ApplyPanelBackground()
        => Background = PanelBackground ?? new SolidColorBrush(IsDark ? _darkPanel : _lightPanel);

    private void ApplyRowAppearance()
    {
        if (_rows is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the rows.
            return;
        }

        var color = EffectiveTextColor;

        foreach (var row in _rows)
        {
            row.Apply(color, FontFamily, FontSize);
        }
    }

    /// <summary>
    /// One menu row: [icon] text, 44dp, press pulse; dismisses the menu, then activates.
    /// The horizontal insets live on the icon and the label, NOT on the row, so the press
    /// highlight paints edge to edge; the row stretches to the panel's width at arrange while
    /// measuring only its content (a horizontal stack, never a star column).
    /// </summary>
    private sealed class Row : Grid
    {
        private readonly Image? _icon;
        private readonly Label _label;
        private readonly Rectangle _pressHighlight;
        private Color _color = ScaffoldNavBarDefaults.Foreground;

        public ToolbarItem Item { get; }

        public Row(ToolbarItem item, bool hasIconColumn, Func<Task> closeAsync)
        {
            Item = item;
            HeightRequest = 44;

            _pressHighlight = new Rectangle { Opacity = 0, InputTransparent = true };
            Add(_pressHighlight);

            var content = new HorizontalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.Center
            };

            if (hasIconColumn)
            {
                _icon = new Image
                {
                    Aspect = Aspect.AspectFit,
                    WidthRequest = 24,
                    HeightRequest = 24,
                    Margin = new Thickness(16, 0, 0, 0),
                    VerticalOptions = LayoutOptions.Center
                };

                content.Add(_icon);
            }

            _label = new Label
            {
                Margin = new Thickness(hasIconColumn ? 12 : 16, 0, 16, 0),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                VerticalOptions = LayoutOptions.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            content.Add(_label);
            Add(content);

            if (item.AutomationId is { } automationId)
            {
                AutomationId = automationId;
            }

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _ = ActivateAsync(closeAsync);
            GestureRecognizers.Add(tap);
            ScaffoldPressable.Observe(content, OnPressedPulse);

            item.PropertyChanged += OnItemPropertyChanged;
            ApplyItem();
        }

        /// <summary>
        /// Content-sized: the row's star column (the only way to stretch the highlight and the tap
        /// target across the panel at arrange) fills whatever bound it is measured with, and the
        /// popup measures the panel against the whole presentation area. Measured UNBOUNDED, the
        /// column collapses to the content, so the panel hugs its widest row; arranged at the
        /// panel's width, the same column stretches the row across it.
        /// </summary>
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            var natural = base.MeasureOverride(double.PositiveInfinity, heightConstraint);

            return new Size(Math.Min(natural.Width, widthConstraint), natural.Height);
        }

        public void Apply(Color color, string? fontFamily, double fontSize)
        {
            _color = color;
            _label.TextColor = color;
            _label.FontFamily = fontFamily;
            _label.FontSize = fontSize;
            _pressHighlight.Fill = new SolidColorBrush(color.WithAlpha(0.14f));
            ApplyItem();
        }

        public void Unsubscribe() => Item.PropertyChanged -= OnItemPropertyChanged;

        private async Task ActivateAsync(Func<Task> closeAsync)
        {
            if (!Item.IsEnabled)
            {
                return;
            }

            // Close first, then activate: an item that navigates would close it anyway, one that
            // opens another overlay must not stack on top of the menu.
            await closeAsync();
            ((IMenuItemController)Item).Activate();
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MenuItem.Text) or nameof(MenuItem.IconImageSource) or nameof(MenuItem.IsEnabled))
            {
                ApplyItem();
            }
        }

        private void ApplyItem()
        {
            _label.Text = Item.Text;

            if (_icon is not null)
            {
                _icon.Source = ScaffoldNavBarDefaults.TintFontIcon(Item.IconImageSource, _color);
            }

            var enabled = Item.IsEnabled;
            IsEnabled = enabled;
            Opacity = enabled ? 1 : 0.38;
        }

        private void OnPressedPulse()
        {
            Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(_pressHighlight);
            _pressHighlight.Opacity = 1;
            _ = _pressHighlight.FadeToAsync(0, 400, Easing.CubicOut);
        }
    }
}
