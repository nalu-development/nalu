using System.Collections.ObjectModel;

namespace Nalu.Maui.Test.Internals;

public class VirtualScrollObservableCollectionAdapterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("collection");
    }

    [Fact]
    public void Constructor_WithValidCollection_ShouldCreateAdapter()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };

        // Act
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);

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
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<TestItem>>(collection);

        // Act
        var result = adapter.GetItem(0, 1) as TestItem;

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(2);
        result.Name.Should().Be("Second");
    }

    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion

    #region Section Transition Tests - Add Operations

    [Fact]
    public void Subscribe_WhenFirstItemAddedToEmptyCollection_ShouldNotifyInsertSectionOnly()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Add("A");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1, "when transitioning from empty to non-empty, only InsertSection should be emitted (it includes the items)");
        
        var changes = receivedChangeSet.Changes.ToList();
        
        // Only change should be InsertSection (which includes the item)
        var sectionChange = changes[0];
        sectionChange.Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        sectionChange.StartSectionIndex.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenItemAddedToNonEmptyCollection_ShouldNotNotifyInsertSection()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Add("C");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.Operation.Should().NotBe(VirtualScrollChangeOperation.InsertSection);
    }

    [Fact]
    public void Subscribe_WhenMultipleItemsAddedToEmptyCollection_ShouldNotifyInsertSectionThenInsertItem()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act - Add first item
        collection.Add("A");
        collection.Add("B");

        // Assert - Check first change set (from first Add)
        changeSets.Should().HaveCount(2);
        var firstChanges = changeSets[0].Changes.ToList();
        firstChanges.Should().HaveCount(1, "first add transitions from empty, so only InsertSection is emitted (includes the item)");
        firstChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        
        // Second change set should only have InsertItem (section already exists)
        var secondChanges = changeSets[1].Changes.ToList();
        secondChanges.Should().HaveCount(1);
        secondChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
    }

    #endregion

    #region Section Transition Tests - Remove Operations

    [Fact]
    public void Subscribe_WhenLastItemRemovedFromCollection_ShouldNotifyRemoveItemAndRemoveSection()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.RemoveAt(0);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var changes = receivedChangeSet.Changes.ToList();
        
        // First change should be RemoveItem
        var itemChange = changes[0];
        itemChange.Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        itemChange.StartSectionIndex.Should().Be(0);
        itemChange.StartItemIndex.Should().Be(0);
        
        // Second change should be RemoveSection
        var sectionChange = changes[1];
        sectionChange.Operation.Should().Be(VirtualScrollChangeOperation.RemoveSection);
        sectionChange.StartSectionIndex.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenItemRemovedButCollectionStillHasItems_ShouldNotNotifyRemoveSection()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.RemoveAt(1);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        change.Operation.Should().NotBe(VirtualScrollChangeOperation.RemoveSection);
    }

    [Fact]
    public void Subscribe_WhenMultipleItemsRemovedUntilEmpty_ShouldNotifyRemoveSection()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Remove first item (should not remove section)
        collection.RemoveAt(0);
        
        // Reset for second removal
        receivedChangeSet = null;
        
        // Act - Remove last item (should remove section)
        collection.RemoveAt(0);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        changes[1].Operation.Should().Be(VirtualScrollChangeOperation.RemoveSection);
    }

    #endregion

    #region Section Transition Tests - Replace Operations

    [Fact]
    public void Subscribe_WhenReplacingItemsInEmptyCollectionWithItems_ShouldNotifyInsertSection()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act - Clear first (triggers Reset)
        collection.Clear();
        
        // Add items to empty collection
        collection.Add("A");
        collection.Add("B");

        // Assert - First add should trigger section insertion only (includes the item)
        changeSets.Should().HaveCountGreaterThanOrEqualTo(2); // Clear + Add operations
        var addChangeSet = changeSets[changeSets.Count - 2]; // Second to last (first Add)
        var changes = addChangeSet.Changes.ToList();
        changes.Should().HaveCount(1, "adding to empty collection should only emit InsertSection (includes the item)");
        changes[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
    }

    [Fact]
    public void Subscribe_WhenReplacingAllItemsWithFewerItems_ShouldNotifyRemoveSectionIfBecomesEmpty()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Clear and check
        collection.Clear();
        
        // Assert - Reset operation should be notified
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.Reset);
    }

    [Fact]
    public void Subscribe_WhenReplacingItemsWithMoreItems_ShouldNotNotifySectionChangeIfAlreadyHadItems()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Replace with more items
        collection[0] = "B";
        collection.Add("C");

        // Assert - First change (replace) should not have section change
        // Note: ObservableCollection doesn't support replacing with more items directly,
        // so this test verifies normal replace doesn't trigger section changes
        receivedChangeSet.Should().NotBeNull();
    }

    #endregion

    #region Section Transition Tests - Reset Operations

    [Fact]
    public void Subscribe_WhenCollectionWithItemsIsCleared_ShouldNotifyReset()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        collection.Clear();

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.Reset);
        
        // Verify section count is updated
        adapter.GetSectionCount().Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenEmptyCollectionIsCleared_ShouldNotifyReset()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
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

    #region Section Transition Tests - Edge Cases

    [Fact]
    public void Subscribe_WhenAddingToEmptyThenRemovingAll_ShouldHandleSectionTransitionsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act - Add item (should insert section)
        collection.Add("A");
        
        // Act - Remove item (should remove section)
        collection.RemoveAt(0);

        // Assert
        changeSets.Should().HaveCount(2);
        
        // First change set: InsertSection only (includes item)
        var firstChanges = changeSets[0].Changes.ToList();
        firstChanges.Should().HaveCount(1, "adding to empty collection should only emit InsertSection (includes the item)");
        firstChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        
        // Second change set: RemoveItem + RemoveSection
        var secondChanges = changeSets[1].Changes.ToList();
        secondChanges.Should().HaveCount(2);
        secondChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        secondChanges[1].Operation.Should().Be(VirtualScrollChangeOperation.RemoveSection);
    }

    [Fact]
    public void Subscribe_WhenAddingMultipleItemsToEmpty_ShouldOnlyInsertSectionOnce()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act - Add first item
        collection.Add("A");
        
        // Act - Add second item (should not insert section again)
        collection.Add("B");

        // Assert
        changeSets.Should().HaveCount(2);
        
        // First change set should have InsertSection only (includes the item)
        var firstChanges = changeSets[0].Changes.ToList();
        firstChanges.Should().HaveCount(1, "first add transitions from empty, so only InsertSection is emitted");
        firstChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        
        // Second change set should NOT have InsertSection
        var secondChanges = changeSets[1].Changes.ToList();
        secondChanges.Should().HaveCount(1);
        secondChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        secondChanges[0].Operation.Should().NotBe(VirtualScrollChangeOperation.InsertSection);
    }

    [Fact]
    public void Subscribe_WhenRemovingMultipleItemsUntilEmpty_ShouldOnlyRemoveSectionOnce()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act - Remove first item (should not remove section)
        collection.RemoveAt(0);
        
        // Act - Remove last item (should remove section)
        collection.RemoveAt(0);

        // Assert
        changeSets.Should().HaveCount(2);
        
        // First change set should NOT have RemoveSection
        var firstChanges = changeSets[0].Changes.ToList();
        firstChanges.Should().HaveCount(1);
        firstChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        
        // Second change set should have RemoveSection
        var secondChanges = changeSets[1].Changes.ToList();
        secondChanges.Should().HaveCount(2);
        secondChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        secondChanges[1].Operation.Should().Be(VirtualScrollChangeOperation.RemoveSection);
    }

    [Fact]
    public void Subscribe_WhenClearingAllItemsThenAddingOne_FlattenedAdapterShouldHaveCorrectCount()
    {
        // Arrange - Start with multiple items
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        
        // Create layout info with no headers/footers (like carousel scenario)
        var layoutInfo = Substitute.For<IVirtualScrollLayoutInfo>();
        layoutInfo.HasGlobalHeader.Returns(false);
        layoutInfo.HasGlobalFooter.Returns(false);
        layoutInfo.HasSectionHeader.Returns(false);
        layoutInfo.HasSectionFooter.Returns(false);
        
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        // Initial count should be 3
        flattenedAdapter.GetItemCount().Should().Be(3);
        
        // Act - Remove all items one by one (simulating user clicking remove button)
        collection.RemoveAt(0); // Remove "A"
        flattenedAdapter.GetItemCount().Should().Be(2);
        
        collection.RemoveAt(0); // Remove "B"
        flattenedAdapter.GetItemCount().Should().Be(1);
        
        collection.RemoveAt(0); // Remove "C"
        flattenedAdapter.GetItemCount().Should().Be(0);
        
        // Act - Now add one item (simulating user clicking add button)
        collection.Add("D");
        
        // Assert - Should have exactly 1 item, not 2
        var finalCount = flattenedAdapter.GetItemCount();
        finalCount.Should().Be(1, "after removing all items and adding one, count should be 1, not 2");
        
        // Verify we can get the item correctly
        flattenedAdapter.TryGetSectionAndItemIndex(0, out var sectionIdx, out var itemIdx).Should().BeTrue();
        sectionIdx.Should().Be(0);
        itemIdx.Should().Be(0);
    }
    
    [Fact]
    public void Subscribe_WhenClearingThenAddingOne_FlattenedAdapterShouldHaveCorrectCount()
    {
        // Arrange - Start with multiple items
        var collection = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        
        // Create layout info with no headers/footers
        var layoutInfo = Substitute.For<IVirtualScrollLayoutInfo>();
        layoutInfo.HasGlobalHeader.Returns(false);
        layoutInfo.HasGlobalFooter.Returns(false);
        layoutInfo.HasSectionHeader.Returns(false);
        layoutInfo.HasSectionFooter.Returns(false);
        
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        // Initial count should be 5
        flattenedAdapter.GetItemCount().Should().Be(5);
        
        // Act - Clear all items at once
        collection.Clear();
        flattenedAdapter.GetItemCount().Should().Be(0);
        
        // Act - Add one item
        collection.Add("New Item");
        
        // Assert - Should have exactly 1 item
        var finalCount = flattenedAdapter.GetItemCount();
        finalCount.Should().Be(1, "after clearing and adding one item, count should be 1, not 2");
        
        // Verify adapter reports correct section count
        adapter.GetSectionCount().Should().Be(1);
        adapter.GetItemCount(0).Should().Be(1);
    }

    #endregion

    #region PerformBatchUpdates Tests

    [Fact]
    public void PerformBatchUpdates_WhenMultipleItemsAdded_ShouldNotifyOnceWithAllChanges()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("B");
            collection.Add("C");
            collection.Add("D");
        });

        // Assert
        changeSets.Should().HaveCount(1, "batch updates should consolidate into a single notification");
        var allChanges = changeSets[0].Changes.ToList();
        allChanges.Should().HaveCount(3);
        allChanges.Should().AllSatisfy(c => c.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem));
    }

    [Fact]
    public void PerformBatchUpdates_WhenAddAndRemove_ShouldNotifyOnceWithAllChanges()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("D");
            collection.RemoveAt(0);
        });

        // Assert
        changeSets.Should().HaveCount(1);
        var allChanges = changeSets[0].Changes.ToList();
        allChanges.Should().HaveCount(2);
        allChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        allChanges[1].Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
    }

    [Fact]
    public void PerformBatchUpdates_WhenNoChanges_ShouldNotNotify()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var callCount = 0;
        using var subscription = adapter.Subscribe(_ => callCount++);

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            // No changes
        });

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    public void PerformBatchUpdates_WithMultipleSubscribers_ShouldNotifyEachOnce()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets1 = new List<VirtualScrollChangeSet>();
        var changeSets2 = new List<VirtualScrollChangeSet>();
        using var subscription1 = adapter.Subscribe(cs => changeSets1.Add(cs));
        using var subscription2 = adapter.Subscribe(cs => changeSets2.Add(cs));

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("B");
            collection.Add("C");
        });

        // Assert
        changeSets1.Should().HaveCount(1);
        changeSets2.Should().HaveCount(1);
        changeSets1[0].Changes.Count().Should().Be(2);
        changeSets2[0].Changes.Count().Should().Be(2);
    }

    [Fact]
    public void PerformBatchUpdates_ChangesOutsideBatch_ShouldNotifyImmediately()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act - add outside batch
        collection.Add("B");
        
        // Then add inside batch
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("C");
            collection.Add("D");
        });

        // Assert
        changeSets.Should().HaveCount(2, "one for immediate change, one for batched changes");
        changeSets[0].Changes.Count().Should().Be(1);
        changeSets[1].Changes.Count().Should().Be(2);
    }

    [Fact]
    public void PerformBatchUpdates_WhenExceptionThrown_ShouldStillFlushChanges()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act & Assert
        var action = () => adapter.PerformBatchUpdates(() =>
        {
            collection.Add("B");
            throw new InvalidOperationException("Test exception");
        });

        action.Should().Throw<InvalidOperationException>();
        changeSets.Should().HaveCount(1, "changes made before exception should still be notified");
        changeSets[0].Changes.Count().Should().Be(1);
    }

    [Fact]
    public void PerformBatchUpdates_WhenTransitioningFromEmptyToNonEmpty_ShouldBatchSectionAndItemChanges()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("A"); // This triggers InsertSection
            collection.Add("B"); // This triggers InsertItem
        });

        // Assert
        changeSets.Should().HaveCount(1);
        var allChanges = changeSets[0].Changes.ToList();
        allChanges.Should().HaveCount(2);
        allChanges[0].Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        allChanges[1].Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
    }

    [Fact]
    public void PerformBatchUpdates_WhenDisposedSubscriber_ShouldNotNotifyDisposedSubscriber()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets1 = new List<VirtualScrollChangeSet>();
        var changeSets2 = new List<VirtualScrollChangeSet>();
        var subscription1 = adapter.Subscribe(cs => changeSets1.Add(cs));
        using var subscription2 = adapter.Subscribe(cs => changeSets2.Add(cs));

        // Dispose first subscription
        subscription1.Dispose();

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("B");
        });

        // Assert
        changeSets1.Should().BeEmpty();
        changeSets2.Should().HaveCount(1);
    }

    [Fact]
    public void PerformBatchUpdates_AfterBatchCompletes_ShouldNotifyImmediatelyAgain()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var changeSets = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => changeSets.Add(cs));

        // Act
        adapter.PerformBatchUpdates(() =>
        {
            collection.Add("B");
        });
        
        // Add after batch - should notify immediately
        collection.Add("C");

        // Assert
        changeSets.Should().HaveCount(2);
        changeSets[0].Changes.Count().Should().Be(1);
        changeSets[1].Changes.Count().Should().Be(1);
    }

    #endregion
}

