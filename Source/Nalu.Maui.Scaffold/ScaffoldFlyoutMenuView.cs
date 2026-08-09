using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The opt-in drawer menu over the owning scaffold's VISIBLE roots — set it as
/// <see cref="Scaffold.FlyoutStart"/> (or any other flyout content level) AND set the matching
/// <see cref="Scaffold.FlyoutStartModeProperty"/> to <c>Flyout</c> (or <c>Auto</c>) to get the
/// default navigation drawer. Rules: an area with a single visible root renders as a flat entry (root
/// title + icon, selected highlight); an area with several visible roots renders its text-only
/// <see cref="ScaffoldArea.Title"/> as a group header with the roots below;
/// <see cref="ScaffoldTabBar"/> areas are excluded unless <see cref="IsTabBarDisplayed"/>.
/// Selection rides <see cref="ScaffoldRoot.SelectCommand"/> (engine-routed, guarded,
/// scaffold-wide busy gate); navigation closes the drawer automatically.
/// Deliberately NOT virtualized: a <see cref="ScrollView"/> over a bindable stack.
/// </summary>
/// <remarks>
/// The menu owns only the drawer surface (<see cref="PanelBackground"/>,
/// <see cref="ContentPadding"/>, <see cref="ItemSpacing"/>). Entry and group-header appearance
/// belongs to <see cref="ScaffoldFlyoutMenuItemView"/> and
/// <see cref="ScaffoldFlyoutMenuGroupHeader"/> — both public, both styled directly with a plain
/// <c>Style</c> (with <c>AppThemeBinding</c> setters for theming).
/// </remarks>
public class ScaffoldFlyoutMenuView : ScrollView
{
    /// <summary>Bindable property for <see cref="PanelBackground"/>.</summary>
    public static readonly BindableProperty PanelBackgroundProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create<Brush?>(
            nameof(PanelBackground),
            defaultValueCreator: static _ => new SolidColorBrush(Colors.White),
            propertyChanged: static menu => (_, value) => menu.Background = value
        );

    /// <summary>Bindable property for <see cref="ContentPadding"/>.</summary>
    public static readonly BindableProperty ContentPaddingProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create(
            nameof(ContentPadding),
            new Thickness(12, 16),
            propertyChanged: static menu => (_, value) => menu._container?.Padding = value
        );

    /// <summary>Bindable property for <see cref="ItemSpacing"/>.</summary>
    public static readonly BindableProperty ItemSpacingProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create(
            nameof(ItemSpacing),
            2.0,
            propertyChanged: static menu => (_, value) => menu._menuStack?.Spacing = value
        );

    /// <summary>Bindable property for <see cref="HeaderView"/>.</summary>
    public static readonly BindableProperty HeaderViewProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create<View?>(
            nameof(HeaderView),
            propertyChanged: static menu => (oldValue, value) => menu.OnHeaderFooterChanged(oldValue, value, header: true)
        );

    /// <summary>Bindable property for <see cref="FooterView"/>.</summary>
    public static readonly BindableProperty FooterViewProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create<View?>(
            nameof(FooterView),
            propertyChanged: static menu => (oldValue, value) => menu.OnHeaderFooterChanged(oldValue, value, header: false)
        );

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create<DataTemplate?>(
            nameof(ItemTemplate),
            propertyChanged: static menu => (_, _) => menu.RebuildItems()
        );

    /// <summary>Bindable property for <see cref="IsTabBarDisplayed"/>.</summary>
    public static readonly BindableProperty IsTabBarDisplayedProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuView>.Create(
            nameof(IsTabBarDisplayed),
            false,
            propertyChanged: static menu => (_, _) => menu.RebuildItems()
        );

    private readonly VerticalStackLayout _container;
    private readonly VerticalStackLayout _menuStack;
    private readonly ObservableCollection<Element> _menuItems = [];
    private readonly List<ScaffoldRoot> _observedRoots = [];
    private Scaffold? _scaffold;

    /// <summary>
    /// Gets or sets the drawer surface background (the flyout presenter mounts the content
    /// edge-to-edge over the scrim). Drives the view's own <see cref="VisualElement.Background"/>
    /// — style THIS, not <c>Background</c>.
    /// </summary>
    public Brush? PanelBackground
    {
        get => (Brush?)GetValue(PanelBackgroundProperty);
        set => SetValue(PanelBackgroundProperty, value);
    }

    /// <summary>Gets or sets the padding around the menu content (header, entries, footer).</summary>
    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    /// <summary>Gets or sets the gap between menu entries.</summary>
    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    /// <summary>Gets or sets the view rendered above the menu (scrolls with it).</summary>
    public View? HeaderView
    {
        get => (View?)GetValue(HeaderViewProperty);
        set => SetValue(HeaderViewProperty, value);
    }

    /// <summary>Gets or sets the view rendered below the menu (scrolls with it).</summary>
    public View? FooterView
    {
        get => (View?)GetValue(FooterViewProperty);
        set => SetValue(FooterViewProperty, value);
    }

    /// <summary>
    /// Gets or sets the template of a root entry; its binding context is the
    /// <see cref="ScaffoldRoot"/>. The template is purely visual — every entry is wrapped in a
    /// tappable container riding <see cref="ScaffoldRoot.SelectCommand"/>; templates needing
    /// their own hit areas can bind that command directly.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether <see cref="ScaffoldTabBar"/> areas appear in the menu (as regular
    /// areas, per the single/multi-root rules). Defaults to false — tabs already have a bar.
    /// </summary>
    public bool IsTabBarDisplayed
    {
        get => (bool)GetValue(IsTabBarDisplayedProperty);
        set => SetValue(IsTabBarDisplayedProperty, value);
    }

    /// <summary>Initializes the drawer menu.</summary>
    public ScaffoldFlyoutMenuView()
    {
        _menuStack = new VerticalStackLayout();
        BindableLayout.SetItemTemplateSelector(_menuStack, new MenuItemTemplateSelector(this));
        BindableLayout.SetItemsSource(_menuStack, _menuItems);

        _container = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { _menuStack }
        };

        Content = _container;

        // Defaults never raise propertyChanged: seed once from the current values (values set
        // by an implicit style land during the BASE ctor, before the subviews existed — the
        // callbacks no-op'd and are made whole here).
        Background = PanelBackground;
        _container.Padding = ContentPadding;
        _menuStack.Spacing = ItemSpacing;
        OnHeaderFooterChanged(null, HeaderView, header: true);
        OnHeaderFooterChanged(null, FooterView, header: false);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        AttachToScaffold(this.GetScaffoldOrDefault());
    }

    private void AttachToScaffold(Scaffold? scaffold)
    {
        if (ReferenceEquals(_scaffold, scaffold))
        {
            return;
        }

        _scaffold = scaffold;
        RebuildItems();
    }

    /// <summary>
    /// Recomputes the flattened menu (group headers + entries) from the scaffold structure.
    /// Root <see cref="ScaffoldRoot.IsVisible"/> changes rebuild live; the areas/roots
    /// structure itself is static after startup.
    /// </summary>
    private void RebuildItems()
    {
        foreach (var observed in _observedRoots)
        {
            observed.PropertyChanged -= OnRootPropertyChanged;
        }

        _observedRoots.Clear();
        _menuItems.Clear();

        if (_scaffold is not { } scaffold)
        {
            return;
        }

        foreach (var area in scaffold.Areas)
        {
            if (area is ScaffoldTabBar && !IsTabBarDisplayed)
            {
                continue;
            }

            foreach (var root in area.Roots)
            {
                root.PropertyChanged += OnRootPropertyChanged;
                _observedRoots.Add(root);
            }

            var visibleRoots = area.Roots.Where(static root => root.IsVisible).ToList();

            if (visibleRoots.Count == 0)
            {
                continue;
            }

            if (visibleRoots.Count > 1)
            {
                // Multi-root area: its text-only Title becomes the group header.
                _menuItems.Add(area);
            }

            foreach (var root in visibleRoots)
            {
                _menuItems.Add(root);
            }
        }
    }

    private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaffoldRoot.IsVisible))
        {
            RebuildItems();
        }
    }

    private void OnHeaderFooterChanged(View? oldView, View? newView, bool header)
    {
        if (_container is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the container.
            return;
        }

        if (oldView is not null)
        {
            _container.Remove(oldView);
        }

        if (newView is not null)
        {
            if (header)
            {
                _container.Insert(0, newView);
            }
            else
            {
                _container.Add(newView);
            }
        }
    }

    /// <summary>Routes group headers and root entries to their templates.</summary>
    private sealed class MenuItemTemplateSelector(ScaffoldFlyoutMenuView owner) : DataTemplateSelector
    {
        private readonly DataTemplate _headerTemplate = new(() => new ScaffoldFlyoutMenuGroupHeader());
        private readonly DataTemplate _entryTemplate = new(() => new ScaffoldFlyoutMenuItemHost(owner));

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => item is ScaffoldArea ? _headerTemplate : _entryTemplate;
    }
}

/// <summary>
/// A multi-root area's group header in the default drawer menu: its text-only
/// <see cref="ScaffoldArea.Title"/> (binding context = the area). Style it with
/// <c>&lt;Style TargetType="nalu:ScaffoldFlyoutMenuGroupHeader"&gt;</c>.
/// </summary>
/// <remarks>
/// It hosts the label rather than deriving from it: a value assigned in a constructor is a
/// MANUAL value and outranks every style setter, so the defaults below live on properties this
/// type owns — which a style can then override.
/// </remarks>
public sealed class ScaffoldFlyoutMenuGroupHeader : Grid
{
    private readonly Label _label;

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuGroupHeader>.Create(
            nameof(TextColor),
            Colors.Gray,
            propertyChanged: static header => (_, value) => header._label?.TextColor = value
        );

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuGroupHeader>.Create<string?>(
            nameof(FontFamily),
            propertyChanged: static header => (_, value) => header._label?.FontFamily = value
        );

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuGroupHeader>.Create(
            nameof(FontSize),
            13.0,
            propertyChanged: static header => (_, value) => header._label?.FontSize = value
        );

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuGroupHeader>.Create(
            nameof(FontAttributes),
            FontAttributes.Bold,
            propertyChanged: static header => (_, value) => header._label?.FontAttributes = value
        );

    /// <summary>Bindable property for <see cref="HeaderPadding"/>.</summary>
    public static readonly BindableProperty HeaderPaddingProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuGroupHeader>.Create(
            nameof(HeaderPadding),
            new Thickness(12, 12, 12, 4),
            propertyChanged: static header => (_, value) => header._label?.Padding = value
        );

    /// <summary>Gets or sets the header text color.</summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the header font family.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the header font size.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the header font attributes.</summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>
    /// Gets or sets the padding around the header text. Drives the inner label's padding —
    /// style THIS, not <c>Padding</c>.
    /// </summary>
    public Thickness HeaderPadding
    {
        get => (Thickness)GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    /// <summary>Initializes the group header.</summary>
    public ScaffoldFlyoutMenuGroupHeader()
    {
        _label = new Label();
        _label.SetBinding(Label.TextProperty, static (ScaffoldArea area) => area.Title);
        Add(_label);

        // Defaults never raise propertyChanged: seed once from the current values.
        _label.TextColor = TextColor;
        _label.FontFamily = FontFamily;
        _label.FontSize = FontSize;
        _label.FontAttributes = FontAttributes;
        _label.Padding = HeaderPadding;
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ScaffoldArea { Title: { Length: > 0 } title })
        {
            AutomationId = $"FlyoutGroup{title}";
        }
    }
}

/// <summary>
/// The tappable wrapper of a drawer menu entry: hosts the user
/// <see cref="ScaffoldFlyoutMenuView.ItemTemplate"/> content (or the default row) and rides
/// <see cref="ScaffoldRoot.SelectCommand"/> — templates stay purely visual.
/// </summary>
internal sealed class ScaffoldFlyoutMenuItemHost : ContentView
{
    public ScaffoldFlyoutMenuItemHost(ScaffoldFlyoutMenuView owner)
    {
        Content = owner.ItemTemplate?.CreateContent() as View ?? new ScaffoldFlyoutMenuItemView();

        var tap = new TapGestureRecognizer();
        tap.SetBinding(TapGestureRecognizer.CommandProperty, static (ScaffoldRoot root) => root.SelectCommand);
        GestureRecognizers.Add(tap);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ScaffoldRoot { Title: { Length: > 0 } title } && AutomationId is null)
        {
            AutomationId = $"FlyoutItem{title}";
        }
    }
}

/// <summary>
/// The default drawer entry row: icon (when set) + title, with a subtle pill highlight while
/// the root is selected. Binding context = the <see cref="ScaffoldRoot"/>; style it with
/// <c>&lt;Style TargetType="nalu:ScaffoldFlyoutMenuItemView"&gt;</c>.
/// </summary>
public sealed class ScaffoldFlyoutMenuItemView : Grid
{
    private static readonly IValueConverter _isNotNullConverter = new IsNotNullConverter();

    private readonly Border _pill;
    private readonly Image _icon;
    private readonly Label _label;
    private readonly HorizontalStackLayout _row;

    /// <summary>Bindable property for <see cref="SelectionBackground"/>.</summary>
    public static readonly BindableProperty SelectionBackgroundProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create<Brush?>(
            nameof(SelectionBackground),
            defaultValueCreator: static _ => new SolidColorBrush(Colors.Gray.WithAlpha(0.18f)),
            propertyChanged: static item => (_, value) => item._pill?.Background = value
        );

    /// <summary>Bindable property for <see cref="SelectionCornerRadius"/>.</summary>
    public static readonly BindableProperty SelectionCornerRadiusProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create(
            nameof(SelectionCornerRadius),
            10.0,
            propertyChanged: static item => (_, value) => item._pill?.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(value) }
        );

    /// <summary>Bindable property for <see cref="IconSize"/>.</summary>
    public static readonly BindableProperty IconSizeProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create(
            nameof(IconSize),
            22.0,
            propertyChanged: static item => (_, value) => item.ApplyIconSize(value)
        );

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create(
            nameof(TextColor),
            ScaffoldNavBarDefaults.Foreground,
            propertyChanged: static item => (_, value) => item._label?.TextColor = value
        );

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create<string?>(
            nameof(FontFamily),
            propertyChanged: static item => (_, value) => item._label?.FontFamily = value
        );

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create(
            nameof(FontSize),
            15.0,
            propertyChanged: static item => (_, value) => item._label?.FontSize = value
        );

    /// <summary>Bindable property for <see cref="ItemPadding"/>.</summary>
    public static readonly BindableProperty ItemPaddingProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create(
            nameof(ItemPadding),
            new Thickness(12, 10),
            propertyChanged: static item => (_, value) => item._row?.Padding = value
        );

    /// <summary>Bindable property for <see cref="Spacing"/>.</summary>
    public static readonly BindableProperty SpacingProperty =
        GenericBindableProperty<ScaffoldFlyoutMenuItemView>.Create(
            nameof(Spacing),
            12.0,
            propertyChanged: static item => (_, value) => item._row?.Spacing = value
        );

    /// <summary>Gets or sets the highlight brush painted while the root is selected.</summary>
    public Brush? SelectionBackground
    {
        get => (Brush?)GetValue(SelectionBackgroundProperty);
        set => SetValue(SelectionBackgroundProperty, value);
    }

    /// <summary>Gets or sets the corner radius of the selection highlight.</summary>
    public double SelectionCornerRadius
    {
        get => (double)GetValue(SelectionCornerRadiusProperty);
        set => SetValue(SelectionCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the icon size (both dimensions). The icon renders untinted.</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Gets or sets the entry label color.</summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the entry label font family.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the entry label font size.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the padding inside the entry row (inside the selection highlight).</summary>
    public Thickness ItemPadding
    {
        get => (Thickness)GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }

    /// <summary>Gets or sets the gap between the icon and the label.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Initializes the default drawer entry row.</summary>
    public ScaffoldFlyoutMenuItemView()
    {
        _pill = new Border
        {
            StrokeThickness = 0,
            InputTransparent = true
        };
        _pill.SetBinding(IsVisibleProperty, static (ScaffoldRoot root) => root.IsSelected);
        Add(_pill);

        _icon = new Image
        {
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center
        };
        _icon.SetBinding(Image.SourceProperty, static (ScaffoldRoot root) => root.CurrentIcon);
        _icon.SetBinding(IsVisibleProperty, static (ScaffoldRoot root) => root.CurrentIcon, converter: _isNotNullConverter);

        _label = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        _label.SetBinding(Label.TextProperty, static (ScaffoldRoot root) => root.Title);

        _row = new HorizontalStackLayout { Children = { _icon, _label } };
        Add(_row);

        // Defaults never raise propertyChanged: seed once from the current values.
        _pill.Background = SelectionBackground;
        _pill.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SelectionCornerRadius) };
        ApplyIconSize(IconSize);
        _label.TextColor = TextColor;
        _label.FontFamily = FontFamily;
        _label.FontSize = FontSize;
        _row.Padding = ItemPadding;
        _row.Spacing = Spacing;
    }

    private void ApplyIconSize(double iconSize)
    {
        if (_icon is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the subviews.
            return;
        }

        _icon.WidthRequest = iconSize;
        _icon.HeightRequest = iconSize;
    }

    private sealed class IsNotNullConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not null;

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}
