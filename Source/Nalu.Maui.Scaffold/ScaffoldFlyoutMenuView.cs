using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;

namespace Nalu;

/// <summary>
/// The opt-in drawer menu over the owning scaffold's VISIBLE roots — set it as
/// <see cref="Scaffold.FlyoutStart"/> (or any other flyout content level) to get the default
/// navigation drawer. Rules: an area with a single visible root renders as a flat entry (root
/// title + icon, selected highlight); an area with several visible roots renders its text-only
/// <see cref="ScaffoldArea.Title"/> as a group header with the roots below;
/// <see cref="ScaffoldTabBar"/> areas are excluded unless <see cref="IsTabBarDisplayed"/>.
/// Selection rides <see cref="ScaffoldRoot.SelectCommand"/> (engine-routed, guarded,
/// scaffold-wide busy gate); navigation closes the drawer automatically.
/// Deliberately NOT virtualized: a <see cref="ScrollView"/> over a bindable stack.
/// </summary>
public class ScaffoldFlyoutMenuView : ScrollView
{
    /// <summary>Bindable property for <see cref="HeaderView"/>.</summary>
    public static readonly BindableProperty HeaderViewProperty =
        BindableProperty.Create(nameof(HeaderView), typeof(View), typeof(ScaffoldFlyoutMenuView), null, propertyChanged: (b, o, n) => ((ScaffoldFlyoutMenuView)b).OnHeaderFooterChanged((View?)o, (View?)n, header: true));

    /// <summary>Bindable property for <see cref="FooterView"/>.</summary>
    public static readonly BindableProperty FooterViewProperty =
        BindableProperty.Create(nameof(FooterView), typeof(View), typeof(ScaffoldFlyoutMenuView), null, propertyChanged: (b, o, n) => ((ScaffoldFlyoutMenuView)b).OnHeaderFooterChanged((View?)o, (View?)n, header: false));

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty =
        BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(ScaffoldFlyoutMenuView), null, propertyChanged: (b, _, _) => ((ScaffoldFlyoutMenuView)b).RebuildItems());

    /// <summary>Bindable property for <see cref="IsTabBarDisplayed"/>.</summary>
    public static readonly BindableProperty IsTabBarDisplayedProperty =
        BindableProperty.Create(nameof(IsTabBarDisplayed), typeof(bool), typeof(ScaffoldFlyoutMenuView), false, propertyChanged: (b, _, _) => ((ScaffoldFlyoutMenuView)b).RebuildItems());

    private readonly VerticalStackLayout _container;
    private readonly VerticalStackLayout _menuStack;
    private readonly ObservableCollection<Element> _menuItems = [];
    private readonly List<ScaffoldRoot> _observedRoots = [];
    private Scaffold? _scaffold;

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
        // The drawer panel behind the menu: an opaque theme-aware background (the flyout
        // presenter mounts the content edge-to-edge over the scrim).
        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1C1C1E")
            : Colors.White;

        _menuStack = new VerticalStackLayout { Spacing = 2 };
        BindableLayout.SetItemTemplateSelector(_menuStack, new MenuItemTemplateSelector(this));
        BindableLayout.SetItemsSource(_menuStack, _menuItems);

        _container = new VerticalStackLayout
        {
            Padding = new Thickness(12, 16),
            Spacing = 8,
            Children = { _menuStack }
        };

        Content = _container;
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        AttachToScaffold(this.FindScaffold());
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
/// <see cref="ScaffoldArea.Title"/> (binding context = the area).
/// </summary>
internal sealed class ScaffoldFlyoutMenuGroupHeader : Label
{
    public ScaffoldFlyoutMenuGroupHeader()
    {
        FontSize = 13;
        FontAttributes = FontAttributes.Bold;
        TextColor = Colors.Gray;
        Padding = new Thickness(12, 12, 12, 4);
        this.SetBinding(TextProperty, static (ScaffoldArea area) => area.Title);
    }

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
/// the root is selected. Binding context = the <see cref="ScaffoldRoot"/>.
/// </summary>
internal sealed class ScaffoldFlyoutMenuItemView : Grid
{
    public ScaffoldFlyoutMenuItemView()
    {
        var pill = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            BackgroundColor = Colors.Gray.WithAlpha(0.18f),
            InputTransparent = true
        };
        pill.SetBinding(IsVisibleProperty, static (ScaffoldRoot root) => root.IsSelected);
        Add(pill);

        var icon = new Image
        {
            WidthRequest = 22,
            HeightRequest = 22,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center
        };
        icon.SetBinding(Image.SourceProperty, static (ScaffoldRoot root) => root.CurrentIcon);
        icon.SetBinding(IsVisibleProperty, static (ScaffoldRoot root) => root.CurrentIcon, converter: _isNotNullConverter);

        var label = new Label
        {
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        label.SetBinding(Label.TextProperty, static (ScaffoldRoot root) => root.Title);

        Add(new HorizontalStackLayout
        {
            Padding = new Thickness(12, 10),
            Spacing = 12,
            Children = { icon, label }
        });
    }

    private static readonly IValueConverter _isNotNullConverter = new IsNotNullConverter();

    private sealed class IsNotNullConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not null;

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}
