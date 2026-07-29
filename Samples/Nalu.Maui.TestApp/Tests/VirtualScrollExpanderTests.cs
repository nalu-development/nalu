using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// VirtualScroll whose items host an <see cref="ExpanderViewBox"/>: expanding an item must
/// grow its cell in place (content measure invalidation propagating to the collection view),
/// pushing the following items down — and collapse must restore them.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll Expander Tests")]
public class VirtualScrollExpanderTests : ContentPage
{
    public VirtualScrollExpanderTests()
    {
        var items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 6).Select(i => new VirtualScrollListItem($"E{i}"))
        );

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "ExpanderScroll",
                                ItemsSource = items,

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var toggleButton = new Button { FontSize = 12, HeightRequest = 36 };
                                        toggleButton.SetBinding(Button.TextProperty, new Binding(nameof(VirtualScrollListItem.Name), stringFormat: "Toggle {0}"));
                                        toggleButton.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollListItem.Name), stringFormat: "Toggle {0}"));

                                        var contentLabel = new Label { FontSize = 13, Margin = new Thickness(8, 4) };
                                        contentLabel.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));

                                        var expander = new ExpanderViewBox
                                                       {
                                                           CollapsedHeight = 60,
                                                           IsExpanded = false,
                                                           BackgroundColor = Colors.LightSteelBlue,
                                                           Content = new VerticalStackLayout
                                                                     {
                                                                         contentLabel,
                                                                         new BoxView
                                                                         {
                                                                             HeightRequest = 240,
                                                                             WidthRequest = 120,
                                                                             HorizontalOptions = LayoutOptions.Start,
                                                                             Color = Colors.SteelBlue
                                                                         }
                                                                     }
                                                       };
                                        expander.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollListItem.Name), stringFormat: "Expander {0}"));

                                        toggleButton.Clicked += (_, _) => expander.IsExpanded = !expander.IsExpanded;

                                        return new VerticalStackLayout { toggleButton, expander };
                                    }
                                )
                            };

        Content = virtualScroll;
    }
}
