using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness for <see cref="SlideBox" />: three lazy slides (B toggleable), a created-counter
/// proving lazy realization + forever-retention + teardown-on-disable, and a peek toggle
/// proving eager neighbor realization only while a peek band is visible.
/// </summary>
[UsedImplicitly]
[TestPage("Slide Box Tests")]
public class SlideBoxTests : ContentPage
{
    private int _createdCount;

    public SlideBoxTests()
    {
        var slideBox = new SlideBox
                       {
                           AutomationId = "SlideBox",
                           HeightRequest = 400
                       };

        var createdLabel = new Label { AutomationId = "SlideCreatedLabel", FontSize = 13, Text = "Created:0" };
        var indexLabel = new Label { AutomationId = "SlideIndexLabel", FontSize = 13, Text = "Index:0" };

        SlideBoxItem MakeItem(string name, Color color)
            => new()
               {
                   Template = new DataTemplate(() =>
                       {
                           createdLabel.Text = $"Created:{++_createdCount}";

                           return new Grid
                                  {
                                      BackgroundColor = color,
                                      Children =
                                      {
                                          new Label
                                          {
                                              Text = name,
                                              AutomationId = $"Slide{name}",
                                              FontSize = 32,
                                              HorizontalOptions = LayoutOptions.Center,
                                              VerticalOptions = LayoutOptions.Center
                                          }
                                      }
                                  };
                       }
                   )
               };

        var itemA = MakeItem("A", Colors.LightSteelBlue);
        var itemB = MakeItem("B", Colors.LightSalmon);
        var itemC = MakeItem("C", Colors.LightSeaGreen);

        slideBox.Items.Add(itemA);
        slideBox.Items.Add(itemB);
        slideBox.Items.Add(itemC);

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
                               MakeButton("Prev", "SlidePrevButton", () => slideBox.Previous()),
                               MakeButton("Next", "SlideNextButton", () => slideBox.Next()),
                               MakeButton("Last", "SlideLastButton", () => slideBox.SelectedIndex = 2),
                               MakeButton("Toggle B", "SlideToggleBButton", () => itemB.IsEnabled = !itemB.IsEnabled),
                               MakeButton("Peek", "SlideTogglePeekButton", () => slideBox.PeekAreaInsets = slideBox.PeekAreaInsets == default ? new Thickness(0, 0, 40, 0) : default),
                               indexLabel,
                               createdLabel
                           }
                       };

        Content = new Grid
                  {
                      RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)],
                      Children = { controls }
                  };

        ((Grid) Content).Add(slideBox, 0, 1);
    }
}
