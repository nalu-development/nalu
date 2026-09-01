using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

public class VirtualScrollSelfDragItem(string name)
{
    public string Name { get; } = name;
    public string CellId => $"SelfCell{Name}";
}

public sealed class VirtualScrollSelfDragModel : INotifyPropertyChanged
{
    private ObservableCollection<VirtualScrollSelfDragItem> _items;
    private string _order = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<VirtualScrollSelfDragItem> Items
    {
        get => _items;
        private set
        {
            _items.CollectionChanged -= OnItemsChanged;
            _items = value;
            _items.CollectionChanged += OnItemsChanged;
            OnPropertyChanged();
            UpdateOrder();
        }
    }

    public string Order
    {
        get => _order;
        private set
        {
            _order = value;
            OnPropertyChanged();
        }
    }

    public VirtualScrollSelfDragModel()
    {
        _items = CreateItems("A", "B", "C", "D", "E", "F", "G", "H");
        _items.CollectionChanged += OnItemsChanged;
        UpdateOrder();
    }

    /// <summary>
    /// Replaces the collection INSTANCE: the coerced adapter is recreated and the Self-bound
    /// DragHandler must follow it — a later drag must reorder the NEW collection, which is what
    /// the Order label mirrors. Distinct item names keep the fresh cells' AutomationIds unique
    /// against detached cells of the previous collection.
    /// </summary>
    public void ReplaceItems() => Items = CreateItems("I", "J", "K", "L", "M", "N", "O", "P");

    private static ObservableCollection<VirtualScrollSelfDragItem> CreateItems(params string[] names)
        => new(names.Select(name => new VirtualScrollSelfDragItem(name)));

    private void OnItemsChanged(object? sender, EventArgs e) => UpdateOrder();

    private void UpdateOrder() => Order = string.Join(",", _items.Select(i => i.Name));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Harness for the auto-wrapped drag&amp;drop pattern through the XAML SOURCE GENERATOR:
/// a plain ObservableCollection bound to ItemsSource and
/// <c>DragHandler="{Binding Adapter, Source={RelativeSource Self}}"</c> as a compiled binding.
/// The Verify button reports whether DragHandler currently IS the coerced Adapter; the Replace
/// button swaps the collection instance to prove the binding follows adapter recreation.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll Self Drag Tests")]
public partial class VirtualScrollSelfDragTests : ContentPage
{
    private readonly VirtualScrollSelfDragModel _model = new();

    public VirtualScrollSelfDragTests()
    {
        InitializeComponent();
        BindingContext = _model;

#if IOS || MACCATALYST
        // A real held long-press cannot be injected from the test host on Apple platforms:
        // this button drives the library's drag pipeline through the internal simulator.
        var simButton = new Button { Text = "A→D", AutomationId = "SimSelfDragAD", FontSize = 11 };
        simButton.Clicked += async (_, _) => await VirtualScrollDragSimulator.SimulateDragAsync(Scroll, 0, 4);
        ButtonsRow.Add(simButton);
#endif
    }

    private void OnVerifyClicked(object? sender, EventArgs e)
        => StatusLabel.Text = $"adapter:{(Scroll.Adapter is null ? "null" : "ok")}"
            + $" reorder:{Scroll.Adapter is IReorderableVirtualScrollAdapter}"
            + $" same:{ReferenceEquals(Scroll.DragHandler, Scroll.Adapter) && Scroll.DragHandler is not null}";

    private void OnReplaceClicked(object? sender, EventArgs e)
    {
        _model.ReplaceItems();
        StatusLabel.Text = string.Empty;
    }
}
