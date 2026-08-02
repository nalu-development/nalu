using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class ScaffoldImeHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushEntries() => navigationService.GoToAsync(Navigation.Relative().Push<ScaffoldImeEntryPageModel>());
}

[UsedImplicitly]
public partial class ScaffoldImeEntryPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

public sealed class ScaffoldImeListItem(int index)
{
    public string Name { get; } = $"Item {index}";
    public string EntryId { get; } = $"ScaffoldImeItemEntry{index}";
}

file static class ScaffoldImeFactory
{
    /// <summary>The app-reset escape hatch NaluApp.ResetAsync relies on for Scaffold-hosted pages (no decorated ResetButton).</summary>
    public static Button MakeExitButton(string marker)
    {
        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

/// <summary>
/// Root page: pushes the entry page through the Nalu navigation engine. Carries its own
/// HideSoftInputOnTapped + entry: the INITIAL page presented by the scaffold must receive
/// NavigatedTo too (the swap path runs on first display with a null previous page).
/// </summary>
[UsedImplicitly]
public class ScaffoldImeHomePage : ContentPage
{
    public ScaffoldImeHomePage(ScaffoldImeHomePageModel model)
    {
        BindingContext = model;
        Title = "IME Home";
        HideSoftInputOnTapped = true;

        var tapTarget = new Label
        {
            Text = "Tap here to dismiss the keyboard",
            AutomationId = "ScaffoldImeHomeTapTarget",
            HeightRequest = 56,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = Colors.LightGoldenrodYellow
        };

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "ScaffoldImeHome", AutomationId = "ScaffoldImeHome", FontSize = 22, FontAttributes = FontAttributes.Bold },
                new Entry { AutomationId = "ScaffoldImeHomeEntry", Placeholder = "type here", FontSize = 14 },
                tapTarget,
                NavPageFactory.MakeButton("Push entries", "PushScaffoldIme", model.PushEntries),
                ScaffoldImeFactory.MakeExitButton("ScaffoldImeHome")
            }
        };
    }
}

/// <summary>
/// Scaffold-PUSHED page with entries inside virtualized items and
/// <see cref="ContentPage.HideSoftInputOnTapped"/>: the feature is gated on the page's
/// HasNavigatedTo, which MAUI's own hosts set via internal navigation events — the Scaffold
/// raises them through <c>ScaffoldPageNavigationEvents</c>, and this harness proves it.
/// </summary>
[UsedImplicitly]
public class ScaffoldImeEntryPage : ContentPage
{
    public ScaffoldImeEntryPage(ScaffoldImeEntryPageModel model)
    {
        BindingContext = model;
        Title = "IME Entries";
        HideSoftInputOnTapped = true;

        var items = new ObservableCollection<ScaffoldImeListItem>(Enumerable.Range(1, 40).Select(i => new ScaffoldImeListItem(i)));

        var itemTemplate = new DataTemplate(() =>
        {
            var label = new Label { WidthRequest = 96, VerticalOptions = LayoutOptions.Center, FontSize = 13 };
            label.SetBinding(Label.TextProperty, nameof(ScaffoldImeListItem.Name));

            var entry = new Entry { HorizontalOptions = LayoutOptions.Fill, FontSize = 14, Placeholder = "type here" };
            entry.SetBinding(AutomationIdProperty, nameof(ScaffoldImeListItem.EntryId));

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
            AutomationId = "ScaffoldImeList",
            ItemsSource = items,
            ItemTemplate = itemTemplate
        };

        var tapTarget = new Label
        {
            Text = "Tap here to dismiss the keyboard",
            AutomationId = "ScaffoldImeTapTarget",
            HeightRequest = 56,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = Colors.LightGoldenrodYellow
        };

        var controls = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 8),
            Children =
            {
                NavPageFactory.MakeButton("Pop", "PopScaffoldIme", model.Pop),
                ScaffoldImeFactory.MakeExitButton("ScaffoldImeEntries")
            }
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

        grid.Add(controls);
        grid.Add(tapTarget, 0, 1);
        grid.Add(virtualScroll, 0, 2);

        Content = grid;
    }
}

/// <summary>
/// Scaffold harness for soft-keyboard behavior on SCAFFOLD-hosted pages: HideSoftInputOnTapped
/// must work on pages navigated through the Nalu engine, and the keyboard must hide when a
/// focused virtualized item recycles.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold IME Tests")]
public class ImeScaffold : Scaffold
{
    public ImeScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(ScaffoldImeHomePage) });
    }
}
