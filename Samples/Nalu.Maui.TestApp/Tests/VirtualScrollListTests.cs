using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll List Tests")]
public class VirtualScrollListTestsNavigationPage : NavigationPage
{
    public VirtualScrollListTestsNavigationPage() : base(new VirtualScrollListTestsController())
    {
        
    }
}

public class VirtualScrollListTestsController : ContentPage
{
    public VirtualScrollListTestsController()
    {
        var scrollView = new ScrollView();
        var verticalStack = new VerticalStackLayout { Spacing = 8, Padding = 16 };
        scrollView.Content = verticalStack;
        
        var openTestPageButton = new Button { Text = "Open Virtual Scroll List Test Page", AutomationId =  "OpenTestPage" };
        openTestPageButton.Clicked += (_, _) =>
        {
            Navigation.PushAsync(new VirtualScrollListTests());
        };
        
        verticalStack.Add(openTestPageButton);
        
        Content = scrollView;
    }
}

public class VirtualScrollListTests : ContentPage
{
    public VirtualScrollListTests(Action<VirtualScroll>? configure = null)
    {
        BindingContext = new { Header = "The header", Footer = "The footer" };

        var items = Enumerable.Range(1, 10)
                              .Select(i => new { Name = $"Item {i}" })
                              .ToList();

        var virtualScroll = new VirtualScroll
                            {
                                Adapter = items,
                                
                                HeaderTemplate = new DataTemplate(() =>
                                    {
                                        var headerLabel = new Label();
                                        headerLabel.SetBinding(Label.TextProperty, "Header");
                                        headerLabel.AutomationId = "HeaderLabel";
                                        headerLabel.FontAttributes = FontAttributes.Bold;
                                        headerLabel.FontSize = 24;
                                        headerLabel.HorizontalOptions = LayoutOptions.Center;

                                        return headerLabel;
                                    }
                                ),

                                FooterTemplate = new DataTemplate(() =>
                                    {
                                        var footerLabel = new Label();
                                        footerLabel.SetBinding(Label.TextProperty, "Footer");
                                        footerLabel.AutomationId = "FooterLabel";
                                        footerLabel.FontAttributes = FontAttributes.Italic;
                                        footerLabel.FontSize = 18;
                                        footerLabel.HorizontalOptions = LayoutOptions.Center;

                                        return footerLabel;
                                    }
                                ),

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var itemLabel = new Label();
                                        itemLabel.SetBinding(Label.TextProperty, "Name");
                                        itemLabel.SetBinding(AutomationIdProperty, "Name");
                                        itemLabel.FontSize = 18;
                                        itemLabel.Margin = new Thickness(10);

                                        return itemLabel;
                                    }
                                )
                            };
        
        configure?.Invoke(virtualScroll);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(virtualScroll, 0, 1);
        
        Content = grid;
    }
}
