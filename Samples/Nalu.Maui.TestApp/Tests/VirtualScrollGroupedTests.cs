using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

public class VirtualScrollSection(string name, IEnumerable<VirtualScrollListItem> items)
{
    public string Name { get; } = name;
    public ObservableCollection<VirtualScrollListItem> Items { get; } = new(items);
}

[UsedImplicitly]
[TestPage("Virtual Scroll Grouped Tests")]
public class VirtualScrollGroupedTests : ContentPage
{
    private readonly ObservableCollection<VirtualScrollSection> _sections;
    private readonly VirtualScroll _virtualScroll;
    private readonly Entry _sectionEntry;
    private readonly Entry _nameEntry;
    private readonly Label _countLabel;

    public VirtualScrollGroupedTests()
    {
        BindingContext = new { Header = "Grouped header", Footer = "Grouped footer" };

        _sections = new ObservableCollection<VirtualScrollSection>(
            new[] { "A", "B", "C", "D", "E" }.Select(CreateSection)
        );

        var adapter = VirtualScroll.CreateObservableCollectionAdapter(_sections, s => s.Items);

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "GroupedScroll",
                             ItemsSource = adapter,
                             DragHandler = adapter,

                             HeaderTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { AutomationId = "GroupedHeader", FontSize = 22, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center };
                                     label.SetBinding(Label.TextProperty, "Header");

                                     return label;
                                 }
                             ),

                             FooterTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { AutomationId = "GroupedFooter", FontSize = 18, FontAttributes = FontAttributes.Italic, HorizontalOptions = LayoutOptions.Center };
                                     label.SetBinding(Label.TextProperty, "Footer");

                                     return label;
                                 }
                             ),

                             SectionHeaderTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     FontSize = 18,
                                                     FontAttributes = FontAttributes.Bold,
                                                     BackgroundColor = Colors.LightGray,
                                                     Padding = new Thickness(10, 6)
                                                 };
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "Section {0}"));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "Header {0}"));

                                     return label;
                                 }
                             ),

                             SectionFooterTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     FontSize = 14,
                                                     FontAttributes = FontAttributes.Italic,
                                                     BackgroundColor = Colors.WhiteSmoke,
                                                     Padding = new Thickness(10, 4)
                                                 };
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "End of {0}"));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "Footer {0}"));

                                     return label;
                                 }
                             ),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 16, Margin = new Thickness(16, 8) };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        _sectionEntry = new Entry { Placeholder = "Section", AutomationId = "SectionEntry", MinimumWidthRequest = 80 };
        WrapLayout.SetExpandRatio(_sectionEntry, 1);
        _nameEntry = new Entry { Placeholder = "Item name", AutomationId = "NameEntry", MinimumWidthRequest = 80 };
        WrapLayout.SetExpandRatio(_nameEntry, 1);

        _countLabel = new Label { AutomationId = "SectionCountLabel", FontSize = 14 };
        UpdateCount();
        _sections.CollectionChanged += (_, _) => UpdateCount();

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _sectionEntry,
                                 _nameEntry,
                                 MakeButton("Add section", "AddSectionButton", AddSection),
                                 MakeButton("Remove section", "RemoveSectionButton", RemoveSection),
                                 MakeButton("Add item", "AddItemButton", AddItem),
                                 MakeButton("Remove item", "RemoveItemButton", RemoveItem),
                                 MakeButton("Add many", "AddManySectionsButton", AddManySections),
                                 MakeButton("Scroll to section", "ScrollToSectionButton", ScrollToSection),
                                 MakeButton("Scroll to item", "ScrollToItemButton", ScrollToItem),
                                 _countLabel
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

    private static VirtualScrollSection CreateSection(string name)
        => new(name, Enumerable.Range(1, 5).Select(i => new VirtualScrollListItem($"{name}{i}")));

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void UpdateCount() => _countLabel.Text = $"Sections: {_sections.Count}";

    private VirtualScrollSection? FindSection() => _sections.FirstOrDefault(s => s.Name == _sectionEntry.Text?.Trim());

    private void AddSection()
    {
        var name = _sectionEntry.Text?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            _sections.Add(CreateSection(name));
        }
    }

    private void RemoveSection()
    {
        if (FindSection() is { } section)
        {
            _sections.Remove(section);
        }
    }

    private void AddItem()
    {
        var name = _nameEntry.Text?.Trim();

        if (FindSection() is { } section && !string.IsNullOrEmpty(name))
        {
            section.Items.Add(new VirtualScrollListItem(name));
        }
    }

    private void RemoveItem()
    {
        if (FindSection() is { } section
            && section.Items.FirstOrDefault(i => i.Name == _nameEntry.Text?.Trim()) is { } item)
        {
            section.Items.Remove(item);
        }
    }

    private void AddManySections()
    {
        for (var i = 1; i <= 20; i++)
        {
            _sections.Add(CreateSection($"S{i}"));
        }
    }

    private void ScrollToSection()
    {
        if (FindSection() is { } section)
        {
            _virtualScroll.ScrollTo(section, ScrollToPosition.Start, animated: false);
        }
    }

    private void ScrollToItem()
    {
        var item = _sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Name == _nameEntry.Text?.Trim());

        if (item is not null)
        {
            _virtualScroll.ScrollTo(item, ScrollToPosition.Start, animated: false);
        }
    }
}
