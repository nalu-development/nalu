using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Nalu.Maui.Test.Internals;

public class VirtualScrollAdapterCoercionTests
{
    private static IVirtualScrollAdapter? Coerce(object? itemsSource)
    {
        var virtualScroll = new VirtualScroll { ItemsSource = itemsSource };
        return virtualScroll.Adapter;
    }

    [Fact]
    public void NullItemsSource_ShouldProduceNullAdapter() => Coerce(null).Should().BeNull();

    [Fact]
    public void ChangingItemsSource_ShouldRaiseAdapterPropertyChanged()
    {
        var virtualScroll = new VirtualScroll();
        var changedProperties = new List<string?>();
        virtualScroll.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        virtualScroll.ItemsSource = new ObservableCollection<string> { "A" };

        changedProperties.Should().Contain(nameof(VirtualScroll.Adapter));
    }

    [Fact]
    public void AdapterItemsSource_ShouldBeUsedAsIs()
    {
        var adapter = new VirtualScrollListAdapter(new[] { "A", "B" });

        Coerce(adapter).Should().BeSameAs(adapter);
    }

    [Fact]
    public void ObservableCollection_ShouldProduceReorderableAdapter()
    {
        var adapter = Coerce(new ObservableCollection<string> { "A", "B", "C" });

        adapter.Should().BeOfType<VirtualScrollReorderableNotifyCollectionChangedAdapter>();
        adapter.Should().BeAssignableTo<IReorderableVirtualScrollAdapter>();
    }

    [Fact]
    public void ObservableCollectionSubclass_ShouldProduceReorderableAdapter()
    {
        var adapter = Coerce(new CustomObservableCollection { "A", "B", "C" });

        adapter.Should().BeAssignableTo<IReorderableVirtualScrollAdapter>();
    }

    [Fact]
    public void ReadOnlyObservableCollection_ShouldProduceNonReorderableAdapter()
    {
        var source = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = Coerce(new ReadOnlyObservableCollection<string>(source));

        adapter.Should().BeOfType<VirtualScrollNotifyCollectionChangedAdapter>();
        adapter.Should().NotBeAssignableTo<IReorderableVirtualScrollAdapter>();
    }

    [Fact]
    public void PlainEnumerable_ShouldProduceListAdapter()
    {
        var adapter = Coerce(new List<string> { "A", "B", "C" });

        adapter.Should().BeOfType<VirtualScrollListAdapter>();
    }

    [Fact]
    public void UnsupportedItemsSource_ShouldThrowNotSupportedException()
    {
        var action = () => Coerce(new object());

        action.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CoercedAdapter_ShouldTrackObservableCollectionChanges()
    {
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = Coerce(collection)!;
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(changeSets.Add);

        collection.Move(0, 2);

        changeSets.Should().ContainSingle()
                  .Which.Changes.Should().ContainSingle()
                  .Which.Operation.Should().Be(VirtualScrollChangeOperation.MoveItem);
    }

    [Fact]
    public void DragMoveItem_OnObservableCollection_ShouldUseMoveAndSuppressChangeSet()
    {
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = (IReorderableVirtualScrollAdapter)Coerce(collection)!;
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(changeSets.Add);
        var externalEvents = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)collection).CollectionChanged += (_, e) => externalEvents.Add(e.Action);

        adapter.MoveItem(new VirtualScrollDragMoveInfo("A", 0, 0, 0, 2));

        collection.Should().Equal("B", "C", "A");
        externalEvents.Should().Equal(NotifyCollectionChangedAction.Move);
        changeSets.Should().BeEmpty();
    }

    [Fact]
    public void DragMoveItem_OnCustomNotifyingList_ShouldFallBackToRemoveInsertAndSuppressChangeSet()
    {
        var collection = new NotifyingList { "A", "B", "C" };
        var adapter = (IReorderableVirtualScrollAdapter)Coerce(collection)!;
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(changeSets.Add);
        var externalEvents = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => externalEvents.Add(e.Action);

        adapter.MoveItem(new VirtualScrollDragMoveInfo("A", 0, 0, 0, 2));

        collection.Cast<object?>().Should().Equal("B", "C", "A");
        externalEvents.Should().Equal(NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add);
        changeSets.Should().BeEmpty();
    }

    [Fact]
    public void FixedSizeNotifyingList_ShouldProduceNonReorderableAdapter()
    {
        var adapter = Coerce(new NotifyingList(isFixedSize: true) { "A", "B", "C" });

        adapter.Should().BeOfType<VirtualScrollNotifyCollectionChangedAdapter>();
        adapter.Should().NotBeAssignableTo<IReorderableVirtualScrollAdapter>();
    }

    private class CustomObservableCollection : ObservableCollection<string>;

    private sealed class NotifyingList(bool isFixedSize = false) : IList, INotifyCollectionChanged
    {
        private readonly List<object?> _items = [];

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public int Count => _items.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => isFixedSize;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public int Add(object? value)
        {
            _items.Add(value);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, _items.Count - 1));
            return _items.Count - 1;
        }

        public void Insert(int index, object? value)
        {
            _items.Insert(index, value);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
        }

        public void RemoveAt(int index)
        {
            var value = _items[index];
            _items.RemoveAt(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, index));
        }

        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value) => _items.Contains(value);
        public int IndexOf(object? value) => _items.IndexOf(value);
        public void Remove(object? value) => throw new NotSupportedException();
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}
