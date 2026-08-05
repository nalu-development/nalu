using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness for <see cref="SlideBox" />: three lazy slides (B toggleable), a created-counter
/// proving lazy realization + forever-retention + teardown-on-disable, a peek toggle proving
/// eager neighbor realization, and a PLATFORM probe proving the cross-axis safe-area inset is
/// NOT consumed (the slide's platform view sits flush with the physical window bottom).
/// </summary>
[UsedImplicitly]
[TestPage("Slide Box Tests")]
public class SlideBoxTests : ContentPage
{
    private int _createdCount;

    public SlideBoxTests()
    {
        // The page must not consume any inset itself (and a loud background makes any
        // accidental consumption visible as a colored band).
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
        BackgroundColor = Colors.MediumPurple;

        // Distinct backgrounds at every level (page purple, box dark, slides pastel): any
        // accidentally consumed inset shows up as a colored band.
        var slideBox = new SlideBox
                       {
                           AutomationId = "SlideBox",
                           BackgroundColor = Colors.DarkSlateGray
                       };

        var createdLabel = new Label { AutomationId = "SlideCreatedLabel", FontSize = 13, Text = "Created:0" };
        var indexLabel = new Label { AutomationId = "SlideIndexLabel", FontSize = 13, Text = "Index:0" };
        var probeLabel = new Label { AutomationId = "SlideProbeLabel", FontSize = 13, Text = "-" };

        SlideBoxItem MakeItem(string name, Color color)
            => new()
               {
                   Template = new DataTemplate(() =>
                       {
                           createdLabel.Text = $"Created:{++_createdCount}";

                           return new Grid
                                  {
                                      AutomationId = $"SlideRoot{name}",
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
                               MakeButton("Probe", "SlideProbeButton", () => probeLabel.Text = MeasureBottomFlush(slideBox.SelectedItem?.Content)),
                               indexLabel,
                               createdLabel,
                               probeLabel
                           }
                       };

        Content = new Grid
                  {
                      SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),
                      RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                      Children = { controls }
                  };

        ((Grid) Content).Add(slideBox, 0, 1);
    }

    /// <summary>
    /// PLATFORM ground truth for the safe-area contract: measures — in native window
    /// coordinates — whether the slide's platform view sits flush with the physical window
    /// bottom, plus the real bottom system inset (0 means the check would be vacuous).
    /// </summary>
    private static string MeasureBottomFlush(View? slideContent)
    {
#if IOS || MACCATALYST
        if (slideContent?.Handler?.PlatformView is not UIKit.UIView platformView || platformView.Window is not { } window)
        {
            return "Flush:n/a";
        }

        var frameInWindow = platformView.ConvertRectToView(platformView.Bounds, window);
        var windowHeight = window.Bounds.Height;
        var inset = window.SafeAreaInsets.Bottom;
        var flush = Math.Abs(windowHeight - frameInWindow.Bottom) < 1.5;

        return $"Flush:{flush} Inset:{(int) inset}";
#elif ANDROID
        if (slideContent?.Handler?.PlatformView is not Android.Views.View platformView || platformView.RootView is not { } rootView)
        {
            return "Flush:n/a";
        }

        var density = platformView.Resources!.DisplayMetrics!.Density;
        var location = new int[2];
        platformView.GetLocationInWindow(location);
        var bottom = (location[1] + platformView.Height) / density;
        var windowHeight = rootView.Height / density;
        var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(platformView);
        var inset = (insets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars())?.Bottom ?? 0) / density;
        var flush = Math.Abs(windowHeight - bottom) < 1.5;

        return $"Flush:{flush} Inset:{(int) inset}";
#else
        _ = slideContent;

        return "Flush:n/a";
#endif
    }
}
