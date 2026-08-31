using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Repro harness for rapid single-item inserts racing the collection view's initial layout
/// (iOS "_Bug_Detected_In_Client_Of_UICollectionView_Invalid_Batch_Updates" assertion): a burst
/// of individually dispatched Insert(0) calls reaches the platform notifier right after the
/// platform view is created — before UICollectionView has ever loaded its data — mirroring
/// lost-message results syncing line-by-line while a page appears.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll Burst Insert Tests")]
public sealed class VirtualScrollBurstInsertTests : ContentPage
{
    private const int _burstSize = 10;

    private readonly Grid _grid;
    private readonly Label _statusLabel;
    private ObservableCollection<string> _items = [];
    private VirtualScroll _virtualScroll = null!;
    private int _next;
    private int _generation;

    public VirtualScrollBurstInsertTests()
    {
        _statusLabel = new Label { AutomationId = "BurstStatusLabel", FontSize = 13, Text = "Idle" };

        var controlsLayout = new HorizontalStackLayout
                             {
                                 MakeButton("Burst", "BurstButton", () => StartBurst()),
                                 MakeButton("Recreate", "RecreateButton", RecreateAndBurst),
                                 _statusLabel
                             };
        controlsLayout.Spacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        _grid = new Grid
                {
                    RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)]
                };
        _grid.Add(controlsLayout);
        AttachVirtualScroll();

        Content = _grid;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void AttachVirtualScroll()
    {
        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "BurstScroll",
                             ItemsSource = VirtualScroll.CreateObservableCollectionAdapter(_items),
                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 13, Margin = new Thickness(16, 5) };
                                     label.SetBinding(Label.TextProperty, ".");
                                     label.SetBinding(AutomationIdProperty, ".");

                                     return label;
                                 }
                             )
                         };

        // The crash window is between platform-view creation and the collection view's first
        // real layout pass: fire the burst the moment the handler connects.
        _virtualScroll.HandlerChanged += (_, _) =>
        {
            if (_virtualScroll.Handler is not null)
            {
                StartBurst();
            }
        };

        _grid.Add(_virtualScroll, 0, 1);
    }

    private void RecreateAndBurst()
    {
        _grid.Remove(_virtualScroll);
        _items = [];
        AttachVirtualScroll();
    }

    private void StartBurst()
    {
        var generation = ++_generation;
        _statusLabel.Text = "Running";

        // One dispatch per insert: each Insert(0) is its own CollectionChanged notification
        // arriving in its own main-queue drain, like results synced line-by-line.
        for (var i = 0; i < _burstSize; i++)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (generation == _generation)
                    {
                        _items.Insert(0, $"B{++_next}");
                    }
                }
            );
        }

        MainThread.BeginInvokeOnMainThread(() =>
            {
                if (generation == _generation)
                {
                    _statusLabel.Text = $"Done: {_items.Count}";
                }
            }
        );
    }
}
