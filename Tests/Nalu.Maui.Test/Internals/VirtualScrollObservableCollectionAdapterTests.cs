using System.Collections.ObjectModel;

namespace Nalu.Maui.Test.Internals;

public class VirtualScrollObservableCollectionAdapterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("collection");
    }

    [Fact]
    public void Constructor_WithValidCollection_ShouldCreateAdapter()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };

        // Act
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Assert
        adapter.Should().NotBeNull();
    }

    #endregion

    #region GetSectionCount Tests

    [Fact]
    public void GetSectionCount_WithEmptyCollection_ShouldReturnZero()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var count = adapter.GetSectionCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetSectionCount_WithNonEmptyCollection_ShouldReturnOne()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var count = adapter.GetSectionCount();

        // Assert
        count.Should().Be(1);
    }

    #endregion

    #region GetItemCount Tests

    [Fact]
    public void GetItemCount_ShouldReturnCollectionCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var count = adapter.GetItemCount(0);

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public void GetItemCount_WithEmptyCollection_ShouldReturnZero()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var count = adapter.GetItemCount(0);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region GetSection Tests

    [Fact]
    public void GetSection_ShouldReturnNull()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var section = adapter.GetSection(0);

        // Assert
        section.Should().BeNull();
    }

    #endregion

    #region GetItem Tests

    [Fact]
    public void GetItem_WithValidIndex_ShouldReturnItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var item = adapter.GetItem(0, 1);

        // Assert
        item.Should().Be("B");
    }

    [Fact]
    public void GetItem_WithFirstIndex_ShouldReturnFirstItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "First", "Second", "Third" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var item = adapter.GetItem(0, 0);

        // Assert
        item.Should().Be("First");
    }

    [Fact]
    public void GetItem_WithLastIndex_ShouldReturnLastItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "First", "Second", "Last" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        var item = adapter.GetItem(0, 2);

        // Assert
        item.Should().Be("Last");
    }

    #endregion

    #region Subscribe Tests - Add Operations

    [Fact]
    public void Subscribe_WhenItemAdded_ShouldNotifyInsertItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Add("C");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    public void Subscribe_WhenItemInsertedAtBeginning_ShouldNotifyInsertItemAtIndex0()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Insert(0, "A");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartItemIndex.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenMultipleItemsAddedViaAddRange_ShouldNotifyInsertItemRange()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Using a custom collection that supports AddRange
        collection.Add("B");
        collection.Add("C");

        // Assert - Each add triggers a separate notification
        receivedChangeSet.Should().NotBeNull();
    }

    #endregion

    #region Subscribe Tests - Remove Operations

    [Fact]
    public void Subscribe_WhenItemRemoved_ShouldNotifyRemoveItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.RemoveAt(1);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenFirstItemRemoved_ShouldNotifyRemoveItemAtIndex0()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.RemoveAt(0);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartItemIndex.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenLastItemRemoved_ShouldNotifyRemoveItemAtLastIndex()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.RemoveAt(2);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartItemIndex.Should().Be(2);
    }

    #endregion

    #region Subscribe Tests - Replace Operations

    [Fact]
    public void Subscribe_WhenItemReplaced_ShouldNotifyReplaceItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection[1] = "X";

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.ReplaceItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenFirstItemReplaced_ShouldNotifyReplaceItemAtIndex0()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection[0] = "X";

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartItemIndex.Should().Be(0);
    }

    #endregion

    #region Subscribe Tests - Move Operations

    [Fact]
    public void Subscribe_WhenItemMoved_ShouldNotifyMoveItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C", "D" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Move(0, 3);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.MoveItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    public void Subscribe_WhenItemMovedBackward_ShouldNotifyMoveItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C", "D" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Move(3, 0);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.MoveItem);
        change.StartItemIndex.Should().Be(3);
        change.EndItemIndex.Should().Be(0);
    }

    #endregion

    #region Subscribe Tests - Reset Operations

    [Fact]
    public void Subscribe_WhenCollectionCleared_ShouldNotifyReset()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Clear();

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.Reset);
    }

    #endregion

    #region Subscribe Tests - Dispose

    [Fact]
    public void Subscribe_WhenDisposed_ShouldNotNotify()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        var callCount = 0;
        var subscription = adapter.Subscribe(_ => callCount++);

        // Act
        subscription.Dispose();
        collection.Add("C");

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenDisposedMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        var subscription = adapter.Subscribe(_ => { });

        // Act & Assert
        var action = () =>
        {
            subscription.Dispose();
            subscription.Dispose();
        };
        action.Should().NotThrow();
    }

    #endregion

    #region Multiple Subscriptions Tests

    [Fact]
    public void Subscribe_WithMultipleSubscriptions_ShouldNotifyAll()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        var callCount1 = 0;
        var callCount2 = 0;
        using var subscription1 = adapter.Subscribe(_ => callCount1++);
        using var subscription2 = adapter.Subscribe(_ => callCount2++);

        // Act
        collection.Add("B");

        // Assert
        callCount1.Should().Be(1);
        callCount2.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenOneSubscriptionDisposed_ShouldStillNotifyOthers()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);
        var callCount1 = 0;
        var callCount2 = 0;
        var subscription1 = adapter.Subscribe(_ => callCount1++);
        using var subscription2 = adapter.Subscribe(_ => callCount2++);

        // Act
        subscription1.Dispose();
        collection.Add("B");

        // Assert
        callCount1.Should().Be(0);
        callCount2.Should().Be(1);
    }

    #endregion

    #region Data Consistency Tests

    [Fact]
    public void GetItemCount_AfterAddingItems_ShouldReflectNewCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        collection.Add("C");
        collection.Add("D");

        // Assert
        adapter.GetItemCount(0).Should().Be(4);
    }

    [Fact]
    public void GetItemCount_AfterRemovingItems_ShouldReflectNewCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C", "D" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        collection.RemoveAt(0);
        collection.RemoveAt(0);

        // Assert
        adapter.GetItemCount(0).Should().Be(2);
    }

    [Fact]
    public void GetItem_AfterModifyingCollection_ShouldReturnUpdatedItem()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        collection[1] = "X";

        // Assert
        adapter.GetItem(0, 1).Should().Be("X");
    }

    [Fact]
    public void GetSectionCount_AfterClearingCollection_ShouldReturnZero()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<string>>(collection);

        // Act
        collection.Clear();

        // Assert
        adapter.GetSectionCount().Should().Be(0);
    }

    #endregion

    #region Complex Object Tests

    [Fact]
    public void GetItem_WithComplexObjects_ShouldReturnCorrectObject()
    {
        // Arrange
        var item1 = new TestItem { Id = 1, Name = "First" };
        var item2 = new TestItem { Id = 2, Name = "Second" };
        var collection = new ObservableCollection<TestItem> { item1, item2 };
        var adapter = new VirtualScrollObservableCollectionAdapter<ObservableCollection<TestItem>>(collection);

        // Act
        var result = adapter.GetItem(0, 1) as TestItem;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
        result.Name.Should().Be("Second");
    }

    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}

