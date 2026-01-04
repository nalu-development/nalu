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
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
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
                          ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                          Padding = new Thickness(16, 8),
                          Margin = new Thickness(0, 8),
                          BackgroundColor = Colors.Beige,
                          ColumnSpacing = 16
                      };
        runGrid.Add(entry);
        runGrid.Add(button, 1);
        
        grid.Add(runGrid, 0, 1);

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
        
        grid.Add(virtualScroll, 0, 2);
        
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
        Application.Current!.Windows[0].Page = page;
    }
}
