using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll List Tests")]
public class VirtualScrollListTestsNavigationPage : NavigationPage
{
    public VirtualScrollListTestsNavigationPage() : base(new VirtualScrollListTestsController())
    {
    }
}

public class VirtualScrollListTestsController : ContentPage
{
    public VirtualScrollListTestsController()
    {
        var scrollView = new ScrollView();
        var verticalStack = new VerticalStackLayout { Spacing = 8, Padding = 16 };
        scrollView.Content = verticalStack;

        var openTestPageButton = new Button { Text = "Open Virtual Scroll List Test Page", AutomationId = "OpenTestPage" };
        openTestPageButton.Clicked += (_, _) =>
        {
            Navigation.PushAsync(new VirtualScrollListTests());
        };

        verticalStack.Add(openTestPageButton);

        Content = scrollView;
    }
}

public class VirtualScrollListItem(string name)
{
    public string Name { get; } = name;
}

public class VirtualScrollListTests : ContentPage
{
    private readonly ObservableCollection<VirtualScrollListItem> _items;
    private readonly VirtualScroll _virtualScroll;
    private readonly Entry _positionEntry;
    private readonly Entry _extraEntry;
    private readonly Label _countLabel;
    private readonly Label _rangeLabel;

    /// <summary>
    /// Exercises <see cref="DataTemplateSelector"/> support: items whose name starts with
    /// "Special" render with a "★ " prefix.
    /// </summary>
    private sealed class ListItemTemplateSelector : DataTemplateSelector
    {
        public required DataTemplate RegularTemplate { get; init; }
        public required DataTemplate SpecialTemplate { get; init; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
            => item is VirtualScrollListItem { Name: { } name } && name.StartsWith("Special", StringComparison.Ordinal)
                ? SpecialTemplate
                : RegularTemplate;
    }

    public VirtualScrollListTests(Action<VirtualScroll>? configure = null)
    {
        BindingContext = new { Header = "The header", Footer = "The footer" };

        _items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 20).Select(i => new VirtualScrollListItem($"Item {i}"))
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "ListScroll",
                             ItemsSource = _items,

                             HeaderTemplate = new DataTemplate(() =>
                                 {
                                     var headerLabel = new Label();
                                     headerLabel.SetBinding(Label.TextProperty, "Header");
                                     headerLabel.AutomationId = "HeaderLabel";
                                     headerLabel.FontAttributes = FontAttributes.Bold;
                                     headerLabel.FontSize = 24;
                                     headerLabel.HorizontalOptions = LayoutOptions.Center;

                                     return headerLabel;
                                 }
                             ),

                             FooterTemplate = new DataTemplate(() =>
                                 {
                                     var footerLabel = new Label();
                                     footerLabel.SetBinding(Label.TextProperty, "Footer");
                                     footerLabel.AutomationId = "FooterLabel";
                                     footerLabel.FontAttributes = FontAttributes.Italic;
                                     footerLabel.FontSize = 18;
                                     footerLabel.HorizontalOptions = LayoutOptions.Center;

                                     return footerLabel;
                                 }
                             ),

                             ItemTemplate = new ListItemTemplateSelector
                                            {
                                                RegularTemplate = new DataTemplate(() =>
                                                    {
                                                        var itemLabel = new Label();
                                                        itemLabel.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                                        itemLabel.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));
                                                        itemLabel.FontSize = 18;
                                                        itemLabel.Margin = new Thickness(10);

                                                        return itemLabel;
                                                    }
                                                ),
                                                SpecialTemplate = new DataTemplate(() =>
                                                    {
                                                        var itemLabel = new Label();
                                                        itemLabel.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollListItem.Name), stringFormat: "★ {0}"));
                                                        itemLabel.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));
                                                        itemLabel.FontSize = 18;
                                                        itemLabel.FontAttributes = FontAttributes.Bold;
                                                        itemLabel.Margin = new Thickness(10);

                                                        return itemLabel;
                                                    }
                                                )
                                            }
                         };

        configure?.Invoke(_virtualScroll);

        _positionEntry = new Entry { Placeholder = "Position", AutomationId = "PositionEntry", MinimumWidthRequest = 50, Keyboard = Keyboard.Numeric };
        WrapLayout.SetExpandRatio(_positionEntry, 1);
        _extraEntry = new Entry { Placeholder = "Extra", AutomationId = "ExtraEntry", MinimumWidthRequest = 100 };
        WrapLayout.SetExpandRatio(_extraEntry, 1);

        var addItemButton = MakeButton("Add", "AddItemButton", AddItem);
        var removeItemButton = MakeButton("Remove", "RemoveItemButton", RemoveItem);
        var moveItemButton = MakeButton("Move", "SwapItemButton", MoveItem);
        var replaceItemButton = MakeButton("Replace", "ReplaceItemButton", ReplaceItem);
        var clearItemsButton = MakeButton("Clear", "ClearItemsButton", () => _items.Clear());
        var addManyItemsButton = MakeButton("Add many", "AddManyItemsButton", AddManyItems);
        var scrollToItemButton = MakeButton("Scroll to", "ScrollToItemButton", ScrollToItem);
        var switchSourceButton = MakeButton("Static source", "SwitchSourceButton", SwitchToStaticSource);
        var visibleRangeButton = MakeButton("Range", "VisibleRangeButton", UpdateVisibleRange);

        _countLabel = new Label { AutomationId = "ItemCountLabel", FontSize = 14 };
        _rangeLabel = new Label { AutomationId = "VisibleRangeLabel", FontSize = 14, Text = "-" };
        UpdateCount();
        _items.CollectionChanged += (_, _) => UpdateCount();

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _positionEntry,
                                 _extraEntry,
                                 addItemButton,
                                 removeItemButton,
                                 moveItemButton,
                                 replaceItemButton,
                                 clearItemsButton,
                                 addManyItemsButton,
                                 scrollToItemButton,
                                 switchSourceButton,
                                 visibleRangeButton,
                                 _countLabel,
                                 _rangeLabel
                             };
        controlsLayout.BackgroundColor = Colors.White;
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll, 0, 1);

        Content = grid;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private int Position => int.TryParse(_positionEntry.Text, out var position) ? position : _items.Count;

    private string ExtraText => string.IsNullOrWhiteSpace(_extraEntry.Text) ? $"New {_items.Count + 1}" : _extraEntry.Text;

    private void UpdateCount() => _countLabel.Text = $"Count: {_items.Count}";

    private void AddItem() => _items.Insert(Math.Clamp(Position, 0, _items.Count), new VirtualScrollListItem(ExtraText));

    private void RemoveItem()
    {
        var index = Math.Clamp(Position, 0, _items.Count - 1);

        if (_items.Count > 0)
        {
            _items.RemoveAt(index);
        }
    }

    private void MoveItem()
    {
        if (_items.Count < 2 || !int.TryParse(_extraEntry.Text, out var to))
        {
            return;
        }

        var from = Math.Clamp(Position, 0, _items.Count - 1);
        _items.Move(from, Math.Clamp(to, 0, _items.Count - 1));
    }

    private void ReplaceItem()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items[Math.Clamp(Position, 0, _items.Count - 1)] = new VirtualScrollListItem(ExtraText);
    }

    private void AddManyItems()
    {
        var count = int.TryParse(_extraEntry.Text, out var n) ? n : 90;
        var start = _items.Count + 1;

        for (var i = 0; i < count; i++)
        {
            _items.Add(new VirtualScrollListItem($"Item {start + i}"));
        }
    }

    private void ScrollToItem()
    {
        var position = Enum.TryParse<ScrollToPosition>(_extraEntry.Text, true, out var p) ? p : ScrollToPosition.MakeVisible;
        _virtualScroll.ScrollTo(0, Math.Clamp(Position, 0, Math.Max(_items.Count - 1, 0)), position, animated: false);
    }

    private void SwitchToStaticSource()
        // Plain (non-observable) IEnumerable: exercises the VirtualScrollListAdapter path.
        => _virtualScroll.ItemsSource = Enumerable.Range(1, 5)
                                                  .Select(i => new VirtualScrollListItem($"Static {i}"))
                                                  .ToList();

    private void UpdateVisibleRange()
    {
        var range = _virtualScroll.GetVisibleItemsRange();
        _rangeLabel.Text = range is { } r
            ? $"{FormatSection(r.StartSectionIndex)}:{FormatItem(r.StartItemIndex)}-{FormatSection(r.EndSectionIndex)}:{FormatItem(r.EndItemIndex)}"
            : "null";

        static string FormatSection(int index) => index switch
        {
            VirtualScrollRange.GlobalHeaderSectionIndex => "GH",
            VirtualScrollRange.GlobalFooterSectionIndex => "GF",
            _ => index.ToString()
        };

        static string FormatItem(int index) => index switch
        {
            VirtualScrollRange.SectionHeaderItemIndex => "SH",
            VirtualScrollRange.SectionFooterItemIndex => "SF",
            _ => index.ToString()
        };
    }
}
