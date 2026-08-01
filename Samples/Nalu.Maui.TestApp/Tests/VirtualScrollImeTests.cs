using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

public sealed class ImeListItem(int index)
{
    public string Name { get; } = $"Ime Item {index}";
    public string EntryId { get; } = $"ImeItemEntry{index}";
}

/// <summary>
/// Harness for soft-keyboard behavior with entries INSIDE virtualized items (Android focus):
/// the keyboard must hide when the focused item is recycled off-screen, and the page's
/// <see cref="ContentPage.HideSoftInputOnTapped"/> must keep working over the list.
/// Page-side ScrollTo buttons make the recycling deterministic (synthetic swipes vary).
/// NavigationPage + push on purpose: HideSoftInputOnTapped is gated on the page's
/// HasNavigatedTo, which a direct Window.Page swap never sets.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll IME Tests")]
public class VirtualScrollImeTestsNavigationPage : NavigationPage
{
    public VirtualScrollImeTestsNavigationPage()
        : base(new VirtualScrollImeTestsController())
    {
    }
}

public class VirtualScrollImeTestsController : ContentPage
{
    public VirtualScrollImeTestsController()
    {
        var openTestPageButton = new Button { Text = "Open IME Test Page", AutomationId = "OpenTestPage" };
        openTestPageButton.Clicked += (_, _) => Navigation.PushAsync(new VirtualScrollImeTests());

        Content = new VerticalStackLayout { Spacing = 8, Padding = 16, Children = { openTestPageButton } };
    }
}

public class VirtualScrollImeTests : ContentPage
{
    public VirtualScrollImeTests()
    {
        HideSoftInputOnTapped = true;

        var items = new ObservableCollection<ImeListItem>(Enumerable.Range(1, 40).Select(i => new ImeListItem(i)));

        var itemTemplate = new DataTemplate(() =>
        {
            var label = new Label { WidthRequest = 96, VerticalOptions = LayoutOptions.Center, FontSize = 13 };
            label.SetBinding(Label.TextProperty, nameof(ImeListItem.Name));

            var entry = new Entry { HorizontalOptions = LayoutOptions.Fill, FontSize = 14, Placeholder = "type here" };
            entry.SetBinding(AutomationIdProperty, nameof(ImeListItem.EntryId));

            var cell = new Grid
            {
                Padding = new Thickness(16, 6),
                ColumnSpacing = 8,
                ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
            };

            cell.Add(label);
            cell.Add(entry, 1);

            return cell;
        });

        var virtualScroll = new VirtualScroll
        {
            AutomationId = "ImeList",
            ItemsSource = items,
            ItemTemplate = itemTemplate
        };

        var scrollToEndButton = new Button { Text = "Scroll to end", AutomationId = "ImeScrollToEnd", FontSize = 12 };
        scrollToEndButton.Clicked += (_, _) => virtualScroll.ScrollTo(0, items.Count - 1, ScrollToPosition.End, animated: false);

        var scrollToStartButton = new Button { Text = "Scroll to start", AutomationId = "ImeScrollToStart", FontSize = 12 };
        scrollToStartButton.Clicked += (_, _) => virtualScroll.ScrollTo(0, 0, ScrollToPosition.Start, animated: false);

        // A generous tap target OUTSIDE any input: the HideSoftInputOnTapped probe.
        var tapTarget = new Label
        {
            Text = "Tap here to dismiss the keyboard",
            AutomationId = "ImeTapTarget",
            HeightRequest = 56,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = Colors.LightGoldenrodYellow
        };

        var grid = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ]
        };

        // Control specimen OUTSIDE the virtualized list: isolates VirtualScroll-specific IME
        // behavior from page/navigation-level feature preconditions.
        var headerEntry = new Entry { AutomationId = "ImeHeaderEntry", Placeholder = "header entry", WidthRequest = 140, FontSize = 14 };

        var controls = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(16, 8), Children = { scrollToEndButton, scrollToStartButton, headerEntry } };

        grid.Add(controls);
        grid.Add(tapTarget, 0, 1);
        grid.Add(virtualScroll, 0, 2);

        Content = grid;
    }
}
