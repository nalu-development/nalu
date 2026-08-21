using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class HitPageModel : ObservableObject;

/// <summary>
/// Chrome hit-testing harness. Both bars are STRIPS spanning the full width while what you can
/// see of them does not: the tab bar is a centred pill with margins, and a nav bar offset out of
/// the window leaves its whole band empty. The page underneath must receive touches everywhere
/// the chrome is not actually drawn — otherwise the app has invisible dead zones.
/// The page content is deliberately full-bleed (NavBarOverlapsContent, no tab bar inset) so that
/// something of the PAGE sits under every part of both bars.
/// </summary>
[UsedImplicitly]
public class HitPage : ContentPage
{
    public HitPage(HitPageModel model)
    {
        BindingContext = model;
        Title = "Hit";

        Scaffold.SetNavBarOverlapsContent(this, true);

        var offsetButton = new Button { Text = "Offset nav bar", AutomationId = "HitOffsetNavBar", FontSize = 11 };
        offsetButton.Clicked += (_, _) => Scaffold.SetNavBarOffsetY(this, -100);

        var restoreButton = new Button { Text = "Restore nav bar", AutomationId = "HitRestoreNavBar", FontSize = 11 };
        restoreButton.Clicked += (_, _) => Scaffold.SetNavBarOffsetY(this, 0);

        // The catcher spans the WHOLE page, chrome bands included, and COUNTS taps: a touch that
        // passes through a transparent piece of chrome has to land somewhere observable, and the
        // count is the only honest evidence that it did.
        // ONE receiver, and a BoxView rather than a layout or a control: a TapGestureRecognizer
        // on a Grid counts taps on Android and never fires on iOS, while a Button counts them but
        // cannot say WHERE — and adding a recognizer to the button to find out makes the two
        // fight, on Android the recognizer wins the touch and the button's own events stop. A
        // plain drawn view with one recognizer behaves the same on both platforms and reports the
        // position, which is what separates "swallowed" from "aimed a few points off".
        var taps = 0;
        var countLabel = new Label
                         {
                             Text = "taps:0",
                             AutomationId = "HitCount",
                             TextColor = Colors.White,
                             FontSize = 14
                         };

        var receiverTap = new TapGestureRecognizer();

        receiverTap.Tapped += (sender, e) =>
        {
            taps++;
            countLabel.Text = e.GetPosition(sender as View) is { } point
                ? $"taps:{taps} last:{point.X:0},{point.Y:0}"
                : $"taps:{taps}";
        };

        var receiver = new BoxView
                       {
                           AutomationId = "HitButton",
                           Color = Colors.Transparent,
                           GestureRecognizers = { receiverTap }
                       };

        var catcher = new Grid
                      {
                          AutomationId = "HitCatcher",
                          BackgroundColor = Colors.MidnightBlue,

                          // The receiver has to reach UNDER the chrome, which is the whole point
                          // of the harness: by default this grid consumes the safe area as
                          // padding and its children stop short of both bars — the button ended
                          // 4dp above the tab bar pill and 24dp below the nav bar, so every probe
                          // read "swallowed" when it had simply missed.
                          SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),
                          Children =
                          {
                              receiver,
                              new VerticalStackLayout
                              {
                                  Spacing = 6,
                                  Padding = new Thickness(16, 120, 16, 16),
                                  VerticalOptions = LayoutOptions.Start,

                                  // This stack starts at the page's top edge and only its PADDING
                                  // covers the nav bar band. On iOS a layout takes touches across
                                  // its whole bounds, padding included, so it — not the chrome —
                                  // was eating every probe up there. Its buttons keep working.
                                  InputTransparent = true,
                                  CascadeInputTransparent = false,
                                  Children =
                                  {
                                      new Label
                                      {
                                          Text = "HitPage",
                                          AutomationId = "HitPageLabel",
                                          TextColor = Colors.White,
                                          FontSize = 22,
                                          FontAttributes = FontAttributes.Bold
                                      },
                                      countLabel,
                                      offsetButton,
                                      restoreButton,
                                      MakeExitButton("Hit")
                                  }
                              }
                          }
                      };

        Content = catcher;
    }

    /// <summary>Every Scaffold-hosted harness page carries one: it is what NaluApp.ResetAsync taps.</summary>
    private static Button MakeExitButton(string marker)
    {
        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

[TestPage("Scaffold Chrome Hit Tests")]
public class HitScaffold : Scaffold
{
    public HitScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "Hit", PageType = typeof(HitPage) }
                }
            }
        );
    }
}
