using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness for a HORIZONTAL <see cref="SlideBox" /> whose slides each host a vertical
/// <see cref="ScrollView" />: the nested-scrollable case (paged content with scrollable pages).
/// Two questions it answers — does the inner scrollable scroll at all inside a slide, and does
/// the SlideBox's pan recognizer arbitrate correctly with it (vertical drags scroll without
/// changing slide, horizontal drags change slide).
/// </summary>
[UsedImplicitly]
[TestPage("Slide Box Scroll Tests")]
public class SlideBoxScrollTests : ContentPage
{
    public SlideBoxScrollTests()
    {
        var slideBox = new SlideBox
                       {
                           AutomationId = "NestedSlideBox",
                           Orientation = SlideBoxOrientation.Horizontal,
                           BackgroundColor = Colors.DarkSlateGray
                       };

        var indexLabel = new Label { AutomationId = "NestedIndexLabel", FontSize = 13, Text = "Index:0" };

        // Last scroll event across the slides: "<slide>:<y>" — the pollable witness that the
        // inner scrollable actually moved (and which slide it belongs to).
        var scrollLabel = new Label { AutomationId = "NestedScrollLabel", FontSize = 13, Text = "-" };

        SlideBoxItem MakeItem(string name, Color color)
            => new()
               {
                   Template = new DataTemplate(() =>
                       {
                           var stack = new VerticalStackLayout
                                       {
                                           Spacing = 4,
                                           Padding = 12,
                                           Children =
                                           {
                                               new Label
                                               {
                                                   Text = $"Top {name}",
                                                   AutomationId = $"NestedTop{name}",
                                                   FontSize = 20,
                                                   FontAttributes = FontAttributes.Bold
                                               }
                                           }
                                       };

                           // Tall enough to scroll on any phone form factor.
                           for (var i = 0; i < 40; i++)
                           {
                               stack.Add(new Label { Text = $"{name} row {i}", FontSize = 13 });
                           }

                           stack.Add(
                               new Label
                               {
                                   Text = $"Bottom {name}",
                                   AutomationId = $"NestedBottom{name}",
                                   FontSize = 20,
                                   FontAttributes = FontAttributes.Bold
                               }
                           );

                           var scrollView = new ScrollView
                                            {
                                                AutomationId = $"NestedScroll{name}",
                                                BackgroundColor = color,
                                                Content = stack
                                            };

                           scrollView.Scrolled += (_, e) => scrollLabel.Text = $"{name}:{(int) e.ScrollY}";

                           return scrollView;
                       }
                   )
               };

        slideBox.Items.Add(MakeItem("A", Colors.LightSteelBlue));
        slideBox.Items.Add(MakeItem("B", Colors.LightSalmon));
        slideBox.Items.Add(MakeItem("C", Colors.LightSeaGreen));

        slideBox.SelectedIndexChanged += (_, e) => indexLabel.Text = $"Index:{e.NewIndex}";

        Button MakeButton(string text, string automationId, Action onClicked)
        {
            var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
            button.Clicked += (_, _) => onClicked();

            return button;
        }

        var controls = new HorizontalWrapLayout
                       {
                           HorizontalSpacing = 8,
                           VerticalSpacing = 8,
                           Padding = new Thickness(16, 8),
                           Children =
                           {
                               MakeButton("Prev", "NestedPrevButton", () => slideBox.Previous()),
                               MakeButton("Next", "NestedNextButton", () => slideBox.Next()),
                               MakeButton("Swipe off", "NestedToggleSwipeButton", () => slideBox.IsSwipeEnabled = !slideBox.IsSwipeEnabled),
                               indexLabel,
                               scrollLabel
                           }
                       };

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                       Children = { controls }
                   };

        grid.Add(slideBox, 0, 1);

        Content = grid;
    }
}
