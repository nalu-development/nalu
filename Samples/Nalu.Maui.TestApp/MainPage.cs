using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.TestApp;

public class MainPage : ContentPage
{
    public MainPage(IServiceProvider serviceProvider)
    {
        var testPages = typeof(MainPage).Assembly.GetTypes()
                                        .Where(t => t.IsSubclassOf(typeof(Page)) && t.GetCustomAttribute<TestPageAttribute>() is not null)
                                        .Select(t => new TestPageItem(t, t.GetCustomAttribute<TestPageAttribute>()!.Name, () => (Page)ActivatorUtilities.CreateInstance(serviceProvider, t)))
                                        .ToList();

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                       RowSpacing = 8
                   };

        var label = new Label
                    {
                        Text = "Nalu.Maui.TestApp",
                        AutomationId = "AppTitleLabel",
                        FontSize = 24,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };
        grid.Add(label);

        var entry = new Entry
                    {
                        Placeholder = "Type test name here",
                        AutomationId = "TestName",
                        VerticalOptions = LayoutOptions.Center
                    };
        var button = new Button
                     {
                         Text = "Run Test",
                         AutomationId = "RunTestButton",
                         VerticalOptions = LayoutOptions.Center
                     };

        button.Clicked += (_, _) =>
        {
            var testName = entry.Text?.Trim();

            if (string.IsNullOrEmpty(testName))
            {
                return;
            }

            var testPageItem = testPages.FirstOrDefault(t => t.Name.Equals(testName, StringComparison.OrdinalIgnoreCase));

            if (testPageItem != null)
            {
                testPageItem.OpenCommand.Execute(null);
            }
            else
            {
                DisplayAlertAsync("Error", $"Test '{testName}' not found.", "OK");
            }
        };

        var runGrid = new Grid
                      {
                          ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
                          Padding = new Thickness(16, 8),
                          Margin = new Thickness(0, 8),
                          BackgroundColor = Colors.Beige,
                          ColumnSpacing = 16
                      };
        runGrid.Add(entry);
        runGrid.Add(button, 1);

        // Leak verification: forces GC and reports LeakTracker survivors ("Leaked:0" when clean).
        var leaksLabel = new Label
                         {
                             AutomationId = "LeaksLabel",
                             FontSize = 10,
                             VerticalOptions = LayoutOptions.Center
                         };
        var checkLeaksButton = new Button
                               {
                                   Text = "GC",
                                   AutomationId = "CheckLeaksButton",
                                   FontSize = 10,
                                   VerticalOptions = LayoutOptions.Center
                               };

        checkLeaksButton.Clicked += async (_, _) =>
        {
            leaksLabel.Text = "checking";
            leaksLabel.Text = await Task.Run(LeakTracker.CheckAsync);
        };

        // Invisible companion for tests: clears surviving entries after a KNOWN platform
        // leak has been asserted, so later scenarios start from a clean tracker.
        var forgiveLeaksButton = new Button
                                 {
                                     Text = "F",
                                     AutomationId = "ForgiveLeaksButton",
                                     FontSize = 10,
                                     Opacity = 0.2,
                                     VerticalOptions = LayoutOptions.Center
                                 };
        forgiveLeaksButton.Clicked += (_, _) =>
        {
            LeakTracker.Forgive();
            leaksLabel.Text = "forgiven";
        };

        runGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        runGrid.Add(forgiveLeaksButton, 3);
        runGrid.Add(checkLeaksButton, 2);
        grid.Add(runGrid, 0, 1);

        leaksLabel.HorizontalOptions = LayoutOptions.Center;
        grid.Add(leaksLabel, 0, 2);

        var virtualScroll = new VirtualScroll
                            {
                                ItemsSource = testPages,
                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var openTestPageButton = new Button
                                                                 {
                                                                     Margin = new Thickness(16, 8),
                                                                 };

                                        openTestPageButton.SetBinding(Button.TextProperty, nameof(TestPageItem.Name));
                                        openTestPageButton.SetBinding(Button.CommandProperty, nameof(TestPageItem.OpenCommand));

                                        return openTestPageButton;
                                    }
                                ),
                            };

        grid.Add(virtualScroll, 0, 3);

        Content = grid;
    }
}

public partial class TestPageItem
{
    private readonly Func<Page> _factory;
    public Type Type { get; }
    public string Name { get; }

    public TestPageItem(Type type, string name, Func<Page> factory)
    {
        _factory = factory;
        Type = type;
        Name = name;
    }

    [RelayCommand]
    private void Open()
    {
        var page = _factory();
        TestPageDecorator.Decorate(page);
        Application.Current!.Windows[0].Page = page;
    }
}

/// <summary>
/// Adds a small cross-platform "ResetButton" overlay to test pages so that
/// UI tests (DevFlow) can navigate back to the <see cref="MainPage"/> without restarting the app.
/// </summary>
internal static class TestPageDecorator
{
    private const string TestPageRootAutomationId = "TestPageRoot";

    public static void Decorate(Page page)
    {
        switch (page)
        {
            case NavigationPage navigationPage:
                DecorateContentPage(navigationPage.CurrentPage as ContentPage);
                navigationPage.Pushed += (_, e) => DecorateContentPage(e.Page as ContentPage);
                break;

            case ContentPage contentPage:
                DecorateContentPage(contentPage);
                break;

            // Shell / other page types are left untouched:
            // tests relying on them should restart the app to reset state.
        }
    }

    private static void DecorateContentPage(ContentPage? contentPage)
    {
        if (contentPage?.Content is not { } content || content.AutomationId == TestPageRootAutomationId)
        {
            return;
        }

        var resetButton = new Button
                          {
                              Text = "⟲",
                              AutomationId = "ResetButton",
                              FontSize = 14,
                              Padding = new Thickness(0),
                              WidthRequest = 32,
                              HeightRequest = 32,
                              CornerRadius = 16,
                              Opacity = 0.6,
                              BackgroundColor = Colors.Red,
                              TextColor = Colors.White,
                              HorizontalOptions = LayoutOptions.End,
                              VerticalOptions = LayoutOptions.End,
                              Margin = new Thickness(8)
                          };
        resetButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        contentPage.Content = null;

        var grid = new Grid { AutomationId = TestPageRootAutomationId };
        grid.Add(content);
        grid.Add(resetButton);

        contentPage.Content = grid;
    }
}
