using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll Refresh Tests")]
public class VirtualScrollRefreshTests : ContentPage
{
    private readonly ObservableCollection<VirtualScrollListItem> _items;
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _commandCountLabel;
    private readonly Label _eventCountLabel;
    private int _commandCount;
    private int _eventCount;
    private Action? _pendingCompletion;

    public VirtualScrollRefreshTests()
    {
        _items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 30).Select(i => new VirtualScrollListItem($"R{i}"))
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "RefreshScroll",
                             ItemsSource = _items,
                             IsRefreshEnabled = true,
                             RefreshAccentColor = Colors.Purple,

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 16, Margin = new Thickness(16, 10) };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        // The refresh completion callback is delivered as the command parameter:
        // store it so a test can complete the refresh deterministically.
        _virtualScroll.RefreshCommand = new Command<Action>(complete =>
            {
                _commandCount++;
                _pendingCompletion = complete;
                UpdateLabels();
            }
        );

        _virtualScroll.OnRefresh += (_, e) =>
        {
            _eventCount++;
            _pendingCompletion = e.Complete;
            UpdateLabels();
        };

        _commandCountLabel = new Label { AutomationId = "RefreshCommandCountLabel", FontSize = 14 };
        _eventCountLabel = new Label { AutomationId = "RefreshEventCountLabel", FontSize = 14 };

        var isRefreshingLabel = new Label { AutomationId = "IsRefreshingLabel", FontSize = 14 };
        isRefreshingLabel.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScroll.IsRefreshing), source: _virtualScroll, stringFormat: "Refreshing: {0}"));

        UpdateLabels();

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Start refresh", "StartRefreshButton", () => _virtualScroll.IsRefreshing = true),
                                 // Invokes the same pipeline the platform uses when the user pulls to refresh
                                 // (DevFlow synthetic swipes cannot trigger the native UIRefreshControl).
                                 MakeButton("Trigger refresh", "TriggerRefreshButton", () => ((IVirtualScrollController) _virtualScroll).Refresh(() => { })),
                                 MakeButton("Complete refresh", "CompleteRefreshButton", CompleteRefresh),
                                 _commandCountLabel,
                                 _eventCountLabel,
                                 isRefreshingLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll, 0, 1);

        Content = grid;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void UpdateLabels()
    {
        _commandCountLabel.Text = $"Command: {_commandCount}";
        _eventCountLabel.Text = $"Event: {_eventCount}";
    }

    private void CompleteRefresh()
    {
        var completion = _pendingCompletion;
        _pendingCompletion = null;
        completion?.Invoke();

        // Also stop a purely-programmatic indicator (StartRefreshButton) that never went
        // through the platform refresh pipeline.
        _virtualScroll.IsRefreshing = false;
    }
}
