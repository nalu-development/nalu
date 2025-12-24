using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll List Tests")]
public class VirtualScrollListTests : ContentPage
{
    public VirtualScrollListTests()
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

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(virtualScroll, 0, 1);
        
        Content = grid;
    }
}
