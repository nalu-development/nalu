using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Regression harness for the measure/arrange wrap flip: a VirtualScroll cell measured at a
/// FRACTIONAL width (339.1) gets its frame pixel-aligned DOWN by UIKit (339.0 on 2x/3x
/// displays), so a wrap row summing exactly to the measured width (219.1 + 120) re-wraps
/// during arrange while the cell height was computed for a single line.
/// IMPORTANT: the requested width must FIT the window of every test device (375dp on
/// iPhone 11 Pro class, 411.43dp on 420dpi Android): on Android the native measure-spec chain clamps the
/// RecyclerView cells to the window width in a later traversal, so a wider request makes the
/// row legitimately wrap and the test would assert the wrong premise.
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
                                WidthRequest = 339.1,
                                HorizontalOptions = LayoutOptions.Start,

                                ItemsSource = new[] { new object() },

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var wrapLayout = new HorizontalWrapLayout
                                                         {
                                                             AutomationId = "WrapRoundingLayout",
                                                             BackgroundColor = Colors.LightGray
                                                         };

                                        // 219.1 + 120 == 339.1: exactly full at the measured width.
                                        wrapLayout.Add(new BoxView { AutomationId = "WrapA", WidthRequest = 219.1, HeightRequest = 30, Color = Colors.IndianRed });
                                        wrapLayout.Add(new BoxView { AutomationId = "WrapB", WidthRequest = 120, HeightRequest = 30, Color = Colors.SeaGreen });

                                        return wrapLayout;
                                    }
                                )
                            };

        var grid = new Grid();
        grid.Add(virtualScroll);

        Content = grid;
    }
}
