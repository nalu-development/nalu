using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness for VirtualScroll hosted in a PAGER — a container that lays its pages out side by side
/// and brings one into view by scrolling, the way third-party tab views (e.g. Telerik RadTabView)
/// do. Page N therefore rests at N * pageWidth, far outside the window: a regression guard for
/// github.com/nalu-development/nalu/issues/187, where the Android positional safe-area padding
/// turned that rest offset into a padding of whole page widths and collapsed the cells to nothing.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll Pager Tests")]
public class VirtualScrollPagerTests : ContentPage
{
    private const int _pageCount = 3;
    private const int _itemsPerPage = 4;
    private const double _itemExtent = 40;

    private readonly ScrollView _pager;
    private readonly List<View> _pages = [];
    private readonly Label _stateLabel;

    public VirtualScrollPagerTests()
    {
        _stateLabel = new Label { AutomationId = "PagerStateLabel", FontSize = 13 };

        var content = new HorizontalStackLayout { Spacing = 0 };

        for (var pageIndex = 0; pageIndex < _pageCount; pageIndex++)
        {
            var page = CreatePage(pageIndex);
            _pages.Add(page);
            content.Add(page);
        }

        _pager = new ScrollView
                 {
                     AutomationId = "Pager",
                     Orientation = ScrollOrientation.Horizontal,
                     HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                     Content = content
                 };

        // The pages are sized to the viewport only once it is known: that is what puts page N at
        // N * pageWidth in LAYOUT coordinates while the pager scrolls it into view.
        _pager.SizeChanged += (_, _) => ResizePages();

        var buttons = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(16, 8) };

        for (var pageIndex = 0; pageIndex < _pageCount; pageIndex++)
        {
            var index = pageIndex;

            var button = new Button
                         {
                             Text = $"Page {index + 1}",
                             AutomationId = $"PagerGoPage{index}",
                             FontSize = 11
                         };

            button.Clicked += (_, _) => _ = GoToPageAsync(index);
            buttons.Add(button);
        }

        buttons.Add(_stateLabel);

        var grid = new Grid
                   {
                       RowDefinitions =
                       [
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Star)
                       ],
                       Children = { buttons }
                   };

        grid.Add(_pager, 0, 1);

        Content = grid;
        UpdateStateLabel(0);
    }

    private static View CreatePage(int pageIndex)
    {
        var items = Enumerable.Range(0, _itemsPerPage).Select(i => $"Pager{pageIndex}Item{i}").ToList();

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = $"PagerScroll{pageIndex}",
                                BackgroundColor = Colors.LightSteelBlue,
                                ItemsLayout = new VerticalVirtualScrollLayout { EstimatedItemSize = _itemExtent },
                                ItemsSource = items,
                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label
                                                    {
                                                        HeightRequest = _itemExtent,
                                                        FontSize = 13,
                                                        VerticalTextAlignment = TextAlignment.Center
                                                    };

                                        label.SetBinding(Label.TextProperty, ".");
                                        label.SetBinding(AutomationIdProperty, ".");

                                        return label;
                                    }
                                )
                            };

        var page = new Grid
                   {
                       AutomationId = $"PagerPage{pageIndex}",
                       RowDefinitions =
                       [
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Star)
                       ],
                       Children =
                       {
                           new Label
                           {
                               AutomationId = $"PagerPageLabel{pageIndex}",
                               Text = $"Page {pageIndex + 1}",
                               FontSize = 13
                           }
                       }
                   };

        page.Add(virtualScroll, 0, 1);

        return page;
    }

    private void ResizePages()
    {
        var width = _pager.Width;
        var height = _pager.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        foreach (var page in _pages)
        {
            page.WidthRequest = width;
            page.HeightRequest = height;
        }
    }

    private async Task GoToPageAsync(int pageIndex)
    {
        ResizePages();
        await _pager.ScrollToAsync(pageIndex * _pager.Width, 0, false);
        UpdateStateLabel(pageIndex);
    }

    private void UpdateStateLabel(int pageIndex) => _stateLabel.Text = $"Page {pageIndex}";
}
