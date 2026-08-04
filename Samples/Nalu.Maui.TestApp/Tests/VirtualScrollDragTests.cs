using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

public class VirtualScrollDragItem(string name)
{
    public string Name { get; } = name;
    public string CellId => $"DragCell{Name}";
}

/// <summary>
/// Harness for gesture-driven item drag&amp;drop: a flat reorderable adapter wrapped in a
/// recording drag handler. The order label mirrors the LIVE collection order and the status
/// label the lifecycle counts, so tests can assert what a real long-press drag did.
/// The "PIN" item is vetoed by <see cref="IVirtualScrollDragHandler.CanDragItem"/> —
/// the negative case.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll Drag Tests")]
public class VirtualScrollDragTests : ContentPage
{
    private sealed class RecordingDragHandler(IVirtualScrollDragHandler inner, Action stateChanged) : IVirtualScrollDragHandler
    {
        public int StartedCount { get; private set; }
        public int EndedCount { get; private set; }
        public int MoveCount { get; private set; }

        public bool CanDragItem(VirtualScrollDragInfo dragInfo)
            => dragInfo.Item is not VirtualScrollDragItem { Name: "PIN" } && inner.CanDragItem(dragInfo);

        public bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo) => inner.CanDropItemAt(dragDropInfo);

        public void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
        {
            MoveCount++;
            inner.MoveItem(dragMoveInfo);
            stateChanged();
        }

        public void OnDragInitiating(VirtualScrollDragInfo dragInfo)
        {
        }

        public void OnDragStarted(VirtualScrollDragInfo dragInfo)
        {
            StartedCount++;
            stateChanged();
        }

        public void OnDragEnded(VirtualScrollDragInfo virtualScrollDragInfo)
        {
            EndedCount++;
            stateChanged();
        }
    }

    public VirtualScrollDragTests()
    {
        var items = new ObservableCollection<VirtualScrollDragItem>(
            new[] { "A", "B", "PIN", "C", "D", "E", "F", "G" }.Select(name => new VirtualScrollDragItem(name))
        );

        var adapter = VirtualScroll.CreateObservableCollectionAdapter(items);

        var orderLabel = new Label { AutomationId = "DragOrderLabel", FontSize = 13 };
        var statusLabel = new Label { AutomationId = "DragStatusLabel", FontSize = 13 };

        RecordingDragHandler recorder = null!;

        void UpdateLabels()
        {
            orderLabel.Text = string.Join(",", items.Select(i => i.Name));
            statusLabel.Text = $"S:{recorder.StartedCount} E:{recorder.EndedCount} M:{recorder.MoveCount}";
        }

        recorder = new RecordingDragHandler(adapter, UpdateLabels);
        items.CollectionChanged += (_, _) => UpdateLabels();

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "DragScroll",
                                ItemsSource = adapter,
                                DragHandler = recorder,

                                // Tall fixed rows: real long-press drags need forgiving touch
                                // geometry (finger-sized targets, deterministic row math).
                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label
                                        {
                                            FontSize = 20,
                                            VerticalOptions = LayoutOptions.Center,
                                            Margin = new Thickness(24, 0)
                                        };
                                        label.SetBinding(Label.TextProperty, nameof(VirtualScrollDragItem.Name));
                                        label.SetBinding(AutomationIdProperty, nameof(VirtualScrollDragItem.CellId));

                                        var row = new Grid
                                        {
                                            HeightRequest = 70,
                                            BackgroundColor = Colors.Transparent
                                        };
                                        row.Add(new BoxView { Color = Colors.LightGray, HeightRequest = 1, VerticalOptions = LayoutOptions.End });
                                        row.Add(label);

                                        return row;
                                    }
                                )
                            };

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)]
                   };

        var header = new VerticalStackLayout
                     {
                         Padding = new Thickness(16, 8),
                         Spacing = 4,
                         Children = { orderLabel, statusLabel }
                     };

        grid.Add(header);
        grid.Add(virtualScroll, 0, 1);

        Content = grid;

        UpdateLabels();
    }
}
