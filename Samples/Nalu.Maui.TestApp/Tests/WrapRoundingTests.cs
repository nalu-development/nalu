using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Regression harness for the measure/arrange wrap flip: a VirtualScroll cell measured at a
/// FRACTIONAL width (419.1) gets its frame pixel-aligned DOWN by UIKit (419.0 on 2x/3x
/// displays), so a wrap row summing exactly to the measured width (219.1 + 200) re-wraps
/// during arrange while the cell height was computed for a single line.
/// </summary>
[UsedImplicitly]
[TestPage("Wrap Rounding Tests")]
public class WrapRoundingTests : ContentPage
{
    public WrapRoundingTests()
    {
        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "WrapRoundingScroll",
                                WidthRequest = 419.1,
                                HorizontalOptions = LayoutOptions.Start,

                                ItemsSource = new[] { new object() },

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var wrapLayout = new HorizontalWrapLayout
                                                         {
                                                             AutomationId = "WrapRoundingLayout",
                                                             BackgroundColor = Colors.LightGray
                                                         };

                                        // 219.1 + 200 == 419.1: exactly full at the measured width.
                                        wrapLayout.Add(new BoxView { AutomationId = "WrapA", WidthRequest = 219.1, HeightRequest = 30, Color = Colors.IndianRed });
                                        wrapLayout.Add(new BoxView { AutomationId = "WrapB", WidthRequest = 200, HeightRequest = 30, Color = Colors.SeaGreen });

                                        return wrapLayout;
                                    }
                                )
                            };

        var grid = new Grid();
        grid.Add(virtualScroll);

        Content = grid;
    }
}
