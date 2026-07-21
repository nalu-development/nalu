using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll Horizontal Tests")]
public class VirtualScrollHorizontalTests : ContentPage
{
    private readonly ObservableCollection<VirtualScrollListItem> _items;
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _fadingLabel;

    public VirtualScrollHorizontalTests()
    {
        _items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 30).Select(i => new VirtualScrollListItem($"H{i}"))
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "HScroll",
                             HeightRequest = 160,
                             VerticalOptions = LayoutOptions.Start,
                             ItemsSource = _items,
                             ItemsLayout = new HorizontalVirtualScrollLayout { EstimatedItemSize = 100 },

                             HeaderTemplate = new DataTemplate(() => new Label
                                 {
                                     AutomationId = "HHeader",
                                     Text = "H-Head",
                                     WidthRequest = 80,
                                     BackgroundColor = Colors.Gold,
                                     VerticalOptions = LayoutOptions.Fill,
                                     VerticalTextAlignment = TextAlignment.Center,
                                     HorizontalTextAlignment = TextAlignment.Center
                                 }
                             ),

                             FooterTemplate = new DataTemplate(() => new Label
                                 {
                                     AutomationId = "HFooter",
                                     Text = "H-Foot",
                                     WidthRequest = 80,
                                     BackgroundColor = Colors.Silver,
                                     VerticalOptions = LayoutOptions.Fill,
                                     VerticalTextAlignment = TextAlignment.Center,
                                     HorizontalTextAlignment = TextAlignment.Center
                                 }
                             ),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     WidthRequest = 100,
                                                     FontSize = 16,
                                                     VerticalOptions = LayoutOptions.Fill,
                                                     VerticalTextAlignment = TextAlignment.Center,
                                                     HorizontalTextAlignment = TextAlignment.Center,
                                                     BackgroundColor = Colors.LightSteelBlue
                                                 };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        _fadingLabel = new Label { AutomationId = "FadingStateLabel", FontSize = 14, Text = "Fading: 0" };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Scroll to end", "ScrollToEndButton", () => _virtualScroll.ScrollTo(0, _items.Count - 1, ScrollToPosition.End, animated: false)),
                                 MakeButton("Scroll to start", "ScrollToStartButton", () => _virtualScroll.ScrollTo(0, 0, ScrollToPosition.Start, animated: false)),
                                 MakeButton("Toggle fading", "ToggleFadingButton", ToggleFading),
                                 _fadingLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Add(controlsLayout);
        stack.Add(_virtualScroll);

        Content = stack;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void ToggleFading()
    {
        _virtualScroll.FadingEdgeLength = _virtualScroll.FadingEdgeLength > 0 ? 0 : 60;
        _fadingLabel.Text = $"Fading: {_virtualScroll.FadingEdgeLength}";
    }
}
