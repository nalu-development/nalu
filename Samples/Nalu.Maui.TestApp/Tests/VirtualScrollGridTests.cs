using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

public class VirtualScrollGridItem(string name, int lines)
{
    public string Name { get; } = name;

    /// <summary>
    /// Cells in a line are deliberately unequal so the uniform-line rule is observable: a taller
    /// cell must stretch its neighbours to match instead of leaving them ragged.
    /// </summary>
    public string Text { get; } = string.Join('\n', Enumerable.Repeat(name, lines));
}

public class VirtualScrollGridSection(string name, IEnumerable<VirtualScrollGridItem> items)
{
    public string Name { get; } = name;
    public ObservableCollection<VirtualScrollGridItem> Items { get; } = new(items);
}

[UsedImplicitly]
[TestPage("Virtual Scroll Grid Tests")]
public class VirtualScrollGridTests : ContentPage
{
    private readonly ObservableCollection<VirtualScrollGridSection> _sections;
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _configLabel;

    private int _span = 3;
    private double _spacing;
    private bool _horizontal;

    public VirtualScrollGridTests()
    {
        BindingContext = new { Header = "Grid header", Footer = "Grid footer" };

        // Item counts on purpose not multiples of the span: section B must still start on a fresh
        // line rather than filling A's trailing gap.
        _sections = new ObservableCollection<VirtualScrollGridSection>(
            new[]
            {
                CreateSection("A", 5),
                CreateSection("B", 4),
                CreateSection("C", 7)
            }
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "GridScroll",
                             ItemsSource = VirtualScroll.CreateObservableCollectionAdapter(_sections, s => s.Items),
                             ItemsLayout = CreateLayout(),

                             HeaderTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     AutomationId = "GridHeader",
                                                     FontSize = 22,
                                                     FontAttributes = FontAttributes.Bold,
                                                     BackgroundColor = Colors.Gold,
                                                     Padding = new Thickness(10, 8),
                                                     HorizontalTextAlignment = TextAlignment.Center
                                                 };
                                     label.SetBinding(Label.TextProperty, "Header");

                                     return label;
                                 }
                             ),

                             FooterTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     AutomationId = "GridFooter",
                                                     FontSize = 18,
                                                     BackgroundColor = Colors.Silver,
                                                     Padding = new Thickness(10, 8),
                                                     HorizontalTextAlignment = TextAlignment.Center
                                                 };
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
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollGridSection.Name), stringFormat: "Section {0}"));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollGridSection.Name), stringFormat: "GridSectionHeader{0}"));

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
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollGridSection.Name), stringFormat: "End of {0}"));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollGridSection.Name), stringFormat: "GridSectionFooter{0}"));

                                     return label;
                                 }
                             ),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     FontSize = 14,
                                                     Padding = new Thickness(6),
                                                     HorizontalTextAlignment = TextAlignment.Center,
                                                     // Fill so a stretched cell shows it: a cell that
                                                     // did not stretch leaves its background short.
                                                     VerticalOptions = LayoutOptions.Fill,
                                                     HorizontalOptions = LayoutOptions.Fill,
                                                     BackgroundColor = Colors.LightSteelBlue
                                                 };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollGridItem.Text));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollGridItem.Name));

                                     return label;
                                 }
                             )
                         };

        _configLabel = new Label { AutomationId = "GridConfigLabel", FontSize = 14 };
        UpdateConfigLabel();

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Span 2", "GridSpan2Button", () => SetSpan(2)),
                                 MakeButton("Span 3", "GridSpan3Button", () => SetSpan(3)),
                                 MakeButton("Span 4", "GridSpan4Button", () => SetSpan(4)),
                                 MakeButton("Toggle spacing", "GridToggleSpacingButton", ToggleSpacing),
                                 MakeButton("Toggle orientation", "GridToggleOrientationButton", ToggleOrientation),
                                 _configLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll);
        Grid.SetRow(_virtualScroll, 1);

        Content = grid;
    }

    private static VirtualScrollGridSection CreateSection(string name, int itemCount)
        => new(
            name,
            Enumerable.Range(1, itemCount).Select(i => new VirtualScrollGridItem($"{name}{i}", 1 + (i % 3)))
        );

    private GridVirtualScrollLayout CreateLayout()
        => _horizontal
            ? new HorizontalGridVirtualScrollLayout { Span = _span, ItemSpacing = _spacing, LineSpacing = _spacing }
            : new VerticalGridVirtualScrollLayout { Span = _span, ItemSpacing = _spacing, LineSpacing = _spacing };

    private void SetSpan(int span)
    {
        _span = span;
        ApplyLayout();
    }

    private void ToggleSpacing()
    {
        _spacing = _spacing > 0 ? 0 : 12;
        ApplyLayout();
    }

    private void ToggleOrientation()
    {
        _horizontal = !_horizontal;
        ApplyLayout();
    }

    // The grid properties are read when the layout is applied, so a change means a new instance.
    private void ApplyLayout()
    {
        _virtualScroll.ItemsLayout = CreateLayout();
        UpdateConfigLabel();
    }

    private void UpdateConfigLabel()
        => _configLabel.Text = $"Span: {_span}, Spacing: {_spacing}, {(_horizontal ? "Horizontal" : "Vertical")}";

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }
}
