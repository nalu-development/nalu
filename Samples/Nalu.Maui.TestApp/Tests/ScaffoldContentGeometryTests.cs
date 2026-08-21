using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

file static class GeometryPageFactory
{
    /// <summary>Every Scaffold-hosted harness page carries one: it is what NaluApp.ResetAsync taps.</summary>
    public static Button MakeExitButton(string marker)
    {
        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

[UsedImplicitly]
public partial class GeometryHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<GeometryDetailPageModel>());
}

[UsedImplicitly]
public partial class GeometryDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>
/// Root page of the content-geometry harness: a nav bar over a SCROLLABLE root whose content is
/// taller than the window. The shape matters — the page's top inset is its bar's footprint, and a
/// scrollable consumes that inset as content padding, so anything that loses or re-derives the
/// inset across a navigation moves this first label. The DailyHelper reported exactly that:
/// after popping back, the header had slid under the bar.
/// </summary>
[UsedImplicitly]
public class GeometryHomePage : ContentPage
{
    public GeometryHomePage(GeometryHomePageModel model)
    {
        BindingContext = model;
        Title = "Geometry Home";

        var pushButton = NavPageFactory.MakeButton("Push detail", "GeoPushDetail", model.PushDetail);

        Content = new ScrollView
                  {
                      AutomationId = "GeoScroll",
                      Content = new VerticalStackLayout
                                {
                                    Spacing = 6,
                                    Padding = 16,
                                    Children =
                                    {
                                        new Label
                                        {
                                            Text = "GeoTopLabel",
                                            AutomationId = "GeoTopLabel",
                                            FontSize = 22,
                                            FontAttributes = FontAttributes.Bold
                                        },
                                        pushButton,
                                        GeometryPageFactory.MakeExitButton("GeoHome"),

                                        // Taller than any window: the scrollable really scrolls,
                                        // so the inset is real padding rather than slack.
                                        new ViewBox
                                        {
                                            AutomationId = "GeoFiller",
                                            HeightRequest = 2000,
                                            BackgroundColor = Colors.Blue
                                        },
                                        new Label { Text = "GeoEndLabel", AutomationId = "GeoEndLabel", FontSize = 16 }
                                    }
                                }
                  };
    }
}

/// <summary>Pushed page: nothing but a way back.</summary>
[UsedImplicitly]
public class GeometryDetailPage : ContentPage
{
    public GeometryDetailPage(GeometryDetailPageModel model)
    {
        BindingContext = model;
        Title = "Geometry Detail";

        var popButton = NavPageFactory.MakeButton("Pop", "GeoPopDetail", model.Pop);

        // Exactly the DailyHelper's detail shape: the bar overlaps the content (no top inset of
        // its own) and the hero flies in from the page that pushed it.
        Scaffold.SetNavBarOverlapsContent(this, true);

        Content = new VerticalStackLayout
                  {
                      Spacing = 6,
                      Padding = 16,
                      Children =
                      {
                          GeometryHero.MakeHero("GeoDetailHero"),
                          new Label
                          {
                              Text = "GeoDetailPage",
                              AutomationId = "GeoDetailPage",
                              FontSize = 22,
                              FontAttributes = FontAttributes.Bold
                          },
                          popButton,
                          GeometryPageFactory.MakeExitButton("GeoDetail")
                      }
                  };
    }
}

[TestPage("Scaffold Content Geometry Tests")]
public class GeometryScaffold : Scaffold
{
    public GeometryScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Geometry", PageType = typeof(GeometryHomePage) });
    }
}

[UsedImplicitly]
public partial class GeometryVirtualHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<GeometryDetailPageModel>());
}

/// <summary>
/// The same shape as <see cref="GeometryHomePage"/> with the scrollable the DailyHelper actually
/// uses: a VirtualScroll whose header carries the first label. Its Java layer deliberately
/// isolates itself from the net10 insets storm (requestFitSystemWindows no-ops, dispatch is
/// blocked), so it is the harness that says whether the page is INSETTED CORRECTLY THE FIRST
/// TIME rather than corrected a layout pass later.
/// </summary>
[UsedImplicitly]
public class GeometryVirtualHomePage : ContentPage
{
    public GeometryVirtualHomePage(GeometryVirtualHomePageModel model)
    {
        BindingContext = model;
        Title = "Geometry Virtual Home";

        var items = new System.Collections.ObjectModel.ObservableCollection<string>(
            Enumerable.Range(1, 60).Select(i => $"Row {i}")
        );

        Content = new VirtualScroll
                  {
                      AutomationId = "GeoVirtualScroll",
                      ItemsSource = items,
                      HeaderTemplate = new DataTemplate(() => new VerticalStackLayout
                                                              {
                                                                  Spacing = 6,
                                                                  Padding = 16,
                                                                  Children =
                                                                  {
                                                                      new Label
                                                                      {
                                                                          Text = "GeoVirtualTopLabel",
                                                                          AutomationId = "GeoVirtualTopLabel",
                                                                          FontSize = 22,
                                                                          FontAttributes = FontAttributes.Bold
                                                                      },
                                                                      GeometryHero.MakeHero("GeoVirtualHero"),
                                                                      NavPageFactory.MakeButton("Push detail", "GeoVirtualPushDetail", model.PushDetail),
                                                                      GeometryPageFactory.MakeExitButton("GeoVirtualHome")
                                                                  }
                                                              }),
                      ItemTemplate = new DataTemplate(() =>
                          {
                              var label = new Label { FontSize = 16, Padding = new Thickness(16, 10) };
                              label.SetBinding(Label.TextProperty, ".");

                              return label;
                          })
                  };
    }
}

/// <summary>
/// The DailyHelper's hero: the element the user actually taps, paired by TransitionName with one
/// on the detail page so the push flies rather than slides. The flight re-parents live views into
/// the presenter's overlay and hands them back — worth having in the harness that asks whether a
/// page comes back the way it left.
/// </summary>
file static class GeometryHero
{
    public static View MakeHero(string automationId)
    {
        var hero = new BoxView
                   {
                       AutomationId = automationId,
                       Color = Colors.SteelBlue,
                       HeightRequest = 120
                   };

        Scaffold.SetTransitionName(hero, "geo-hero");

        return hero;
    }
}

[TestPage("Scaffold Content Geometry Virtual Tests")]
public class GeometryVirtualScaffold : Scaffold
{
    public GeometryVirtualScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Geometry", PageType = typeof(GeometryVirtualHomePage) });
    }
}
