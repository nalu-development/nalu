using System.Collections.ObjectModel;

namespace Nalu.Maui.Test.Internals;

public class VirtualScrollGroupedObservableCollectionAdapterTests
{
    #region Test Helpers

    private class TestSection
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<string> Items { get; set; } = new();
    }

    private static VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<TestSection>, ObservableCollection<string>> CreateAdapter(
        ObservableCollection<TestSection> sections)
        => new(sections, section => ((TestSection)section).Items);

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSections_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<TestSection>, ObservableCollection<string>>(
            null!,
            section => ((TestSection)section).Items);
        action.Should().Throw<ArgumentNullException>().WithParameterName("sections");
    }

    [Fact]
    public void Constructor_WithNullSectionItemsGetter_ShouldThrowArgumentNullException()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>();

        // Act & Assert
        var action = () => new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<TestSection>, ObservableCollection<string>>(
            sections,
            null!);
        action.Should().Throw<ArgumentNullException>().WithParameterName("sectionItemsGetter");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateAdapter()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A", "B" } }
        };

        // Act
        var adapter = CreateAdapter(sections);

        // Assert
        adapter.Should().NotBeNull();
    }

    #endregion

    #region GetSectionCount Tests

    [Fact]
    public void GetSectionCount_WithEmptySections_ShouldReturnZero()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>();
        var adapter = CreateAdapter(sections);

        // Act
        var count = adapter.GetSectionCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetSectionCount_WithMultipleSections_ShouldReturnCorrectCount()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } },
            new() { Name = "Section3", Items = new ObservableCollection<string> { "C" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var count = adapter.GetSectionCount();

        // Assert
        count.Should().Be(3);
    }

    #endregion

    #region GetItemCount Tests

    [Fact]
    public void GetItemCount_WithValidSectionIndex_ShouldReturnItemCount()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "D", "E" } }
        };
        var adapter = CreateAdapter(sections);

        // Act & Assert
        adapter.GetItemCount(0).Should().Be(3);
        adapter.GetItemCount(1).Should().Be(2);
    }

    [Fact]
    public void GetItemCount_WithNegativeIndex_ShouldReturnZero()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var count = adapter.GetItemCount(-1);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetItemCount_WithIndexOutOfRange_ShouldReturnZero()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var count = adapter.GetItemCount(10);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetItemCount_WithEmptySection_ShouldReturnZero()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "EmptySection", Items = new ObservableCollection<string>() }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var count = adapter.GetItemCount(0);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region GetSection Tests

    [Fact]
    public void GetSection_WithValidIndex_ShouldReturnSection()
    {
        // Arrange
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        var section2 = new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "B" } };
        var sections = new ObservableCollection<TestSection> { section1, section2 };
        var adapter = CreateAdapter(sections);

        // Act
        var result = adapter.GetSection(1);

        // Assert
        result.Should().BeSameAs(section2);
    }

    [Fact]
    public void GetSection_WithNegativeIndex_ShouldReturnNull()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var result = adapter.GetSection(-1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSection_WithIndexOutOfRange_ShouldReturnNull()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var result = adapter.GetSection(10);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetItem Tests

    [Fact]
    public void GetItem_WithValidIndices_ShouldReturnItem()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "D", "E" } }
        };
        var adapter = CreateAdapter(sections);

        // Act & Assert
        adapter.GetItem(0, 1).Should().Be("B");
        adapter.GetItem(1, 0).Should().Be("D");
    }

    [Fact]
    public void GetItem_WithNegativeSectionIndex_ShouldReturnNull()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var result = adapter.GetItem(-1, 0);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetItem_WithNegativeItemIndex_ShouldReturnNull()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var result = adapter.GetItem(0, -1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetItem_WithItemIndexOutOfRange_ShouldReturnNull()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A", "B" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        var result = adapter.GetItem(0, 10);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Subscribe Tests - Section Add Operations

    [Fact]
    public void Subscribe_WhenSectionAdded_ShouldNotifyInsertSection()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.Add(new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "B" } });

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        change.StartSectionIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenSectionInsertedAtBeginning_ShouldNotifyInsertSectionAtIndex0()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.Insert(0, new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } });

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        change.StartSectionIndex.Should().Be(0);
    }

    #endregion

    #region Subscribe Tests - Section Remove Operations

    [Fact]
    public void Subscribe_WhenSectionRemoved_ShouldNotifyRemoveSection()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.RemoveAt(0);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.RemoveSection);
        change.StartSectionIndex.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenLastSectionRemoved_ShouldNotifyRemoveSectionAtLastIndex()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.RemoveAt(1);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartSectionIndex.Should().Be(1);
    }

    #endregion

    #region Subscribe Tests - Section Replace Operations

    [Fact]
    public void Subscribe_WhenSectionReplaced_ShouldNotifyReplaceSection()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections[0] = new TestSection { Name = "NewSection1", Items = new ObservableCollection<string> { "X" } };

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.ReplaceSection);
        change.StartSectionIndex.Should().Be(0);
    }

    #endregion

    #region Subscribe Tests - Section Move Operations

    [Fact]
    public void Subscribe_WhenSectionMoved_ShouldNotifyMoveSection()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } },
            new() { Name = "Section3", Items = new ObservableCollection<string> { "C" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.Move(0, 2);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.MoveSection);
        change.StartSectionIndex.Should().Be(0);
        change.EndSectionIndex.Should().Be(2);
    }

    #endregion

    #region Subscribe Tests - Section Reset Operations

    [Fact]
    public void Subscribe_WhenSectionsCleared_ShouldNotifyReset()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } },
            new() { Name = "Section2", Items = new ObservableCollection<string> { "B" } }
        };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.Clear();

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.Reset);
    }

    #endregion

    #region Subscribe Tests - Item Add Operations

    [Fact]
    public void Subscribe_WhenItemAddedToSection_ShouldNotifyInsertItem()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        section.Items.Add("C");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    public void Subscribe_WhenItemAddedToSecondSection_ShouldNotifyInsertItemWithCorrectSectionIndex()
    {
        // Arrange
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        var section2 = new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "B" } };
        var sections = new ObservableCollection<TestSection> { section1, section2 };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        section2.Items.Add("C");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartSectionIndex.Should().Be(1);
        change.StartItemIndex.Should().Be(1);
    }

    #endregion

    #region Subscribe Tests - Item Remove Operations

    [Fact]
    public void Subscribe_WhenItemRemovedFromSection_ShouldNotifyRemoveItem()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        section.Items.RemoveAt(1);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.RemoveItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    #endregion

    #region Subscribe Tests - Item Replace Operations

    [Fact]
    public void Subscribe_WhenItemReplacedInSection_ShouldNotifyReplaceItem()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        section.Items[1] = "X";

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.ReplaceItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    #endregion

    #region Subscribe Tests - Item Move Operations

    [Fact]
    public void Subscribe_WhenItemMovedInSection_ShouldNotifyMoveItem()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C", "D" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        section.Items.Move(0, 3);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.MoveItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3);
    }

    #endregion

    #region Subscribe Tests - Item Reset Operations

    [Fact]
    public void Subscribe_WhenSectionItemsCleared_ShouldNotifyReplaceSection()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        section.Items.Clear();

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.ReplaceSection);
        change.StartSectionIndex.Should().Be(0);
    }

    #endregion

    #region Subscribe Tests - Dispose

    [Fact]
    public void Subscribe_WhenDisposed_ShouldNotNotifyOnSectionChanges()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);
        var callCount = 0;
        var subscription = adapter.Subscribe(_ => callCount++);

        // Act
        subscription.Dispose();
        sections.Add(new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "B" } });

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenDisposed_ShouldNotNotifyOnItemChanges()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);
        var callCount = 0;
        var subscription = adapter.Subscribe(_ => callCount++);

        // Act
        subscription.Dispose();
        section.Items.Add("B");

        // Assert
        callCount.Should().Be(0);
    }

    #endregion

    #region Subscription Management Tests - Section Index Shifting

    [Fact]
    public void Subscribe_WhenSectionAddedBefore_ShouldStillTrackItemChangesInShiftedSection()
    {
        // Arrange
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        var sections = new ObservableCollection<TestSection> { section1 };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Insert a new section at the beginning
        var section0 = new TestSection { Name = "Section0", Items = new ObservableCollection<string> { "X" } };
        sections.Insert(0, section0);
        receivedChangeSet = null;

        // Act - modify items in the original section (now at index 1)
        section1.Items.Add("B");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartSectionIndex.Should().Be(1); // Section shifted from 0 to 1
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenSectionRemovedBefore_ShouldStillTrackItemChangesInShiftedSection()
    {
        // Arrange
        var section0 = new TestSection { Name = "Section0", Items = new ObservableCollection<string> { "X" } };
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        var sections = new ObservableCollection<TestSection> { section0, section1 };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Remove the first section
        sections.RemoveAt(0);
        receivedChangeSet = null;

        // Act - modify items in the remaining section (now at index 0)
        section1.Items.Add("B");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartSectionIndex.Should().Be(0); // Section shifted from 1 to 0
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenSectionReplaced_ShouldTrackItemChangesInNewSection()
    {
        // Arrange
        var section0 = new TestSection { Name = "Section0", Items = new ObservableCollection<string> { "A" } };
        var sections = new ObservableCollection<TestSection> { section0 };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Replace the section
        var newSection = new TestSection { Name = "NewSection", Items = new ObservableCollection<string> { "X" } };
        sections[0] = newSection;
        receivedChangeSet = null;

        // Act - modify items in the new section
        newSection.Items.Add("Y");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenSectionReplaced_ShouldNotTrackItemChangesInOldSection()
    {
        // Arrange
        var section0 = new TestSection { Name = "Section0", Items = new ObservableCollection<string> { "A" } };
        var sections = new ObservableCollection<TestSection> { section0 };
        var adapter = CreateAdapter(sections);
        var callCount = 0;
        using var subscription = adapter.Subscribe(_ => callCount++);

        // Replace the section
        var newSection = new TestSection { Name = "NewSection", Items = new ObservableCollection<string> { "X" } };
        sections[0] = newSection;
        callCount = 0; // Reset after replace notification

        // Act - modify items in the OLD section (should not notify)
        section0.Items.Add("B");

        // Assert
        callCount.Should().Be(0);
    }

    #endregion

    #region Data Consistency Tests

    [Fact]
    public void GetSectionCount_AfterAddingSection_ShouldReflectNewCount()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>
        {
            new() { Name = "Section1", Items = new ObservableCollection<string> { "A" } }
        };
        var adapter = CreateAdapter(sections);

        // Act
        sections.Add(new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "B" } });

        // Assert
        adapter.GetSectionCount().Should().Be(2);
    }

    [Fact]
    public void GetItemCount_AfterAddingItemToSection_ShouldReflectNewCount()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);

        // Act
        section.Items.Add("C");
        section.Items.Add("D");

        // Assert
        adapter.GetItemCount(0).Should().Be(4);
    }

    [Fact]
    public void GetItem_AfterModifyingItem_ShouldReturnUpdatedItem()
    {
        // Arrange
        var section = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A", "B", "C" } };
        var sections = new ObservableCollection<TestSection> { section };
        var adapter = CreateAdapter(sections);

        // Act
        section.Items[1] = "X";

        // Assert
        adapter.GetItem(0, 1).Should().Be("X");
    }

    #endregion

    #region Multiple Sections Item Changes Tests

    [Fact]
    public void Subscribe_WhenItemsAddedToMultipleSections_ShouldNotifyForEach()
    {
        // Arrange
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        var section2 = new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "B" } };
        var sections = new ObservableCollection<TestSection> { section1, section2 };
        var adapter = CreateAdapter(sections);
        var receivedChanges = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => receivedChanges.Add(cs));

        // Act
        section1.Items.Add("A2");
        section2.Items.Add("B2");

        // Assert
        receivedChanges.Should().HaveCount(2);
        
        var change1 = receivedChanges[0].Changes.First();
        change1.StartSectionIndex.Should().Be(0);
        change1.StartItemIndex.Should().Be(1);
        
        var change2 = receivedChanges[1].Changes.First();
        change2.StartSectionIndex.Should().Be(1);
        change2.StartItemIndex.Should().Be(1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Subscribe_WhenSectionAddedToEmptyCollection_ShouldNotifyInsertSection()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>();
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Act
        sections.Add(new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } });

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertSection);
        change.StartSectionIndex.Should().Be(0);
    }

    [Fact]
    public void Subscribe_WhenItemAddedToNewlyAddedSection_ShouldNotifyInsertItem()
    {
        // Arrange
        var sections = new ObservableCollection<TestSection>();
        var adapter = CreateAdapter(sections);
        var receivedChanges = new List<VirtualScrollChangeSet>();
        using var subscription = adapter.Subscribe(cs => receivedChanges.Add(cs));

        var newSection = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "A" } };
        sections.Add(newSection);
        receivedChanges.Clear();

        // Act
        newSection.Items.Add("B");

        // Assert
        receivedChanges.Should().HaveCount(1);
        var change = receivedChanges[0].Changes.First();
        change.Operation.Should().Be(VirtualScrollChangeOperation.InsertItem);
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenSectionMovedForward_ShouldTrackItemChangesAtNewIndex()
    {
        // Arrange
        var section0 = new TestSection { Name = "Section0", Items = new ObservableCollection<string> { "A" } };
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "B" } };
        var section2 = new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "C" } };
        var sections = new ObservableCollection<TestSection> { section0, section1, section2 };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Move section0 to the end
        sections.Move(0, 2);
        receivedChangeSet = null;

        // Act - modify items in the moved section (now at index 2)
        section0.Items.Add("A2");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartSectionIndex.Should().Be(2);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WhenSectionMovedBackward_ShouldTrackItemChangesAtNewIndex()
    {
        // Arrange
        var section0 = new TestSection { Name = "Section0", Items = new ObservableCollection<string> { "A" } };
        var section1 = new TestSection { Name = "Section1", Items = new ObservableCollection<string> { "B" } };
        var section2 = new TestSection { Name = "Section2", Items = new ObservableCollection<string> { "C" } };
        var sections = new ObservableCollection<TestSection> { section0, section1, section2 };
        var adapter = CreateAdapter(sections);
        VirtualScrollChangeSet? receivedChangeSet = null;
        using var subscription = adapter.Subscribe(cs => receivedChangeSet = cs);

        // Move section2 to the beginning
        sections.Move(2, 0);
        receivedChangeSet = null;

        // Act - modify items in the moved section (now at index 0)
        section2.Items.Add("C2");

        // Assert
        receivedChangeSet.Should().NotBeNull();
        var change = receivedChangeSet!.Changes.First();
        change.StartSectionIndex.Should().Be(0);
        change.StartItemIndex.Should().Be(1);
    }

    #endregion
}

