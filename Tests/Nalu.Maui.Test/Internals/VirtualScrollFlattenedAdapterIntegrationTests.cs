using System.Collections.ObjectModel;

namespace Nalu.Maui.Test.Internals;

/// <summary>
/// Integration tests for VirtualScrollFlattenedAdapter with ObservableCollection adapters.
/// </summary>
public class VirtualScrollFlattenedAdapterIntegrationTests
{
    #region Helper Methods

    private static IVirtualScrollLayoutInfo CreateLayoutInfo(
        bool hasGlobalHeader = false,
        bool hasGlobalFooter = false,
        bool hasSectionHeader = false,
        bool hasSectionFooter = false)
    {
        var layoutInfo = Substitute.For<IVirtualScrollLayoutInfo>();
        layoutInfo.HasGlobalHeader.Returns(hasGlobalHeader);
        layoutInfo.HasGlobalFooter.Returns(hasGlobalFooter);
        layoutInfo.HasSectionHeader.Returns(hasSectionHeader);
        layoutInfo.HasSectionFooter.Returns(hasSectionFooter);
        layoutInfo.Equals(Arg.Any<IVirtualScrollLayoutInfo>()).Returns(call =>
        {
            var other = call.Arg<IVirtualScrollLayoutInfo>();
            return other != null &&
                   other.HasGlobalHeader == hasGlobalHeader &&
                   other.HasGlobalFooter == hasGlobalFooter &&
                   other.HasSectionHeader == hasSectionHeader &&
                   other.HasSectionFooter == hasSectionFooter;
        });
        return layoutInfo;
    }

    #endregion

    #region Single Collection (Non-Grouped) Tests

    [Fact]
    public void SingleCollection_WhenAddingItems_ShouldUpdateFlattenedCount()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItemCount().Should().Be(0, "empty collection should have count 0");

        collection.Add("A");
        flattenedAdapter.GetItemCount().Should().Be(1, "after adding one item");

        collection.Add("B");
        flattenedAdapter.GetItemCount().Should().Be(2, "after adding second item");

        collection.Add("C");
        flattenedAdapter.GetItemCount().Should().Be(3, "after adding third item");
    }

    [Fact]
    public void SingleCollection_WhenRemovingItems_ShouldUpdateFlattenedCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "initial count");

        collection.RemoveAt(1);
        flattenedAdapter.GetItemCount().Should().Be(2, "after removing one item");

        collection.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(1, "after removing second item");

        collection.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(0, "after removing all items");
    }

    [Fact]
    public void SingleCollection_WhenClearingThenAdding_ShouldHaveCorrectCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.Clear();
        var countAfterClear = flattenedAdapter.GetItemCount();

        collection.Add("X");
        var countAfterAdd = flattenedAdapter.GetItemCount();

        // Assert
        countAfterClear.Should().Be(0, "after clearing");
        countAfterAdd.Should().Be(1, "after adding one item to cleared collection");
    }

    [Fact]
    public void SingleCollection_WhenRemovingAllItemsThenAdding_ShouldHaveCorrectCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Remove all items one by one
        while (collection.Count > 0)
        {
            collection.RemoveAt(0);
        }
        var countAfterRemovals = flattenedAdapter.GetItemCount();

        // Add one item
        collection.Add("Z");
        var countAfterAdd = flattenedAdapter.GetItemCount();

        // Assert
        countAfterRemovals.Should().Be(0, "after removing all items");
        countAfterAdd.Should().Be(1, "after adding one item");
    }

    [Fact]
    public void SingleCollection_WhenReplacingItems_ShouldMaintainCorrectCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection[1] = "X";

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "replacing an item should not change count");
        var item = flattenedAdapter.GetItem(1);
        item.Value.Should().Be("X", "replaced item should be accessible");
    }

    [Fact]
    public void SingleCollection_WithSectionHeaders_ShouldIncludeHeaderInCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "2 items + 1 section header");

        // Verify positions
        var header = flattenedAdapter.GetItem(0);
        header.Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);

        var item1 = flattenedAdapter.GetItem(1);
        item1.Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        item1.Value.Should().Be("A");

        var item2 = flattenedAdapter.GetItem(2);
        item2.Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        item2.Value.Should().Be("B");
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndFooter_ShouldIncludeThemInCount()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "2 items + global header + global footer");

        // Verify positions
        var globalHeader = flattenedAdapter.GetItem(0);
        globalHeader.Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);

        var item1 = flattenedAdapter.GetItem(1);
        item1.Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        item1.Value.Should().Be("A");

        var item2 = flattenedAdapter.GetItem(2);
        item2.Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        item2.Value.Should().Be("B");

        var globalFooter = flattenedAdapter.GetItem(3);
        globalFooter.Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithAllHeadersAndFooters_ShouldCountCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "global header + section header + 2 items + section footer + global footer");

        // Verify all positions
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WhenAddingItemsWithHeaders_ShouldMaintainCorrectPositions()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.Add("B");
        collection.Add("C");

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "section header + 3 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
    }

    [Fact]
    public void SingleCollection_WhenMovingItems_ShouldReflectCorrectOrder()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C", "D" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Move "B" (index 1) to position 3
        collection.Move(1, 3);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4);
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("C");
        flattenedAdapter.GetItem(2).Value.Should().Be("D");
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
    }

    [Fact]
    public void SingleCollection_ComplexScenario_MultipleOperations()
    {
        // Arrange
        var collection = new ObservableCollection<string>();
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert - Complex scenario
        // Start empty
        flattenedAdapter.GetItemCount().Should().Be(0);

        // Add 3 items
        collection.Add("A");
        collection.Add("B");
        collection.Add("C");
        flattenedAdapter.GetItemCount().Should().Be(3);

        // Remove middle item
        collection.RemoveAt(1);
        flattenedAdapter.GetItemCount().Should().Be(2);
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("C");

        // Add more items
        collection.Add("D");
        collection.Add("E");
        flattenedAdapter.GetItemCount().Should().Be(4);

        // Replace an item
        collection[2] = "X";
        flattenedAdapter.GetItemCount().Should().Be(4);
        flattenedAdapter.GetItem(2).Value.Should().Be("X");

        // Clear all
        collection.Clear();
        flattenedAdapter.GetItemCount().Should().Be(0);

        // Add one more
        collection.Add("Final");
        flattenedAdapter.GetItemCount().Should().Be(1);
        flattenedAdapter.GetItem(0).Value.Should().Be("Final");
    }

    #endregion

    #region Grouped Collection Tests

    [Fact]
    public void GroupedCollection_WhenAddingSections_ShouldUpdateFlattenedCount()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>();
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItemCount().Should().Be(0);

        sections.Add(new SectionData("Section1", ["A", "B"]));
        flattenedAdapter.GetItemCount().Should().Be(2);

        sections.Add(new SectionData("Section2", ["C", "D", "E"]));
        flattenedAdapter.GetItemCount().Should().Be(5);
    }

    [Fact]
    public void GroupedCollection_WhenRemovingSections_ShouldUpdateFlattenedCount()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C", "D", "E"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItemCount().Should().Be(5);

        sections.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(3);

        sections.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(0);
    }

    [Fact]
    public void GroupedCollection_WhenAddingItemsToSection_ShouldUpdateFlattenedCount()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItemCount().Should().Be(2);

        sections[0].Items.Add("C");
        flattenedAdapter.GetItemCount().Should().Be(3);

        sections[0].Items.Add("D");
        flattenedAdapter.GetItemCount().Should().Be(4);
    }

    [Fact]
    public void GroupedCollection_WhenRemovingItemsFromSection_ShouldUpdateFlattenedCount()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B", "C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItemCount().Should().Be(3);

        sections[0].Items.RemoveAt(1);
        flattenedAdapter.GetItemCount().Should().Be(2);

        sections[0].Items.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(1);

        sections[0].Items.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(0);
    }

    [Fact]
    public void GroupedCollection_WhenClearingAllItemsFromSectionThenAdding_ShouldHaveCorrectCount()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B", "C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        sections[0].Items.Clear();
        var countAfterClear = flattenedAdapter.GetItemCount();

        sections[0].Items.Add("X");
        var countAfterAdd = flattenedAdapter.GetItemCount();

        // Assert
        countAfterClear.Should().Be(0, "after clearing section items");
        countAfterAdd.Should().Be(1, "after adding one item to cleared section");
    }

    [Fact]
    public void GroupedCollection_WithSectionHeaders_ShouldIncludeHeadersInCount()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "2 section headers + 3 items");

        // Verify structure
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(4).Value.Should().Be("C");
    }

    [Fact]
    public void GroupedCollection_WithSectionHeadersAndFooters_ShouldCountCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "section header + 2 items + section footer");

        // Verify structure
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void GroupedCollection_WithAllHeadersAndFooters_ShouldCountCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        // global header + (section1 header + A + section1 footer) + (section2 header + B + section2 footer) + global footer
        flattenedAdapter.GetItemCount().Should().Be(8);

        // Verify structure
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(5).Value.Should().Be("B");
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(7).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WhenMovingSections_ShouldReflectCorrectOrder()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"]),
            new("Section3", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Move Section2 (index 1) to position 0
        sections.Move(1, 0);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3);
        flattenedAdapter.GetItem(0).Value.Should().Be("B");
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
    }

    [Fact]
    public void GroupedCollection_WhenMovingItemsBetweenSections_ShouldUpdateCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C", "D"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Move "B" from Section1 to Section2
        var item = sections[0].Items[1];
        sections[0].Items.RemoveAt(1);
        sections[1].Items.Insert(0, item);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "total items should remain the same");
        
        // Section 1 should have only "A"
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        
        // Section 2 should have "B", "C", "D"
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Value.Should().Be("D");
    }

    [Fact]
    public void GroupedCollection_ComplexScenario_MultipleOperations()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>();
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert - Complex scenario
        // Start empty
        flattenedAdapter.GetItemCount().Should().Be(0);

        // Add section with items
        sections.Add(new SectionData("S1", ["A", "B"]));
        flattenedAdapter.GetItemCount().Should().Be(2);

        // Add another section
        sections.Add(new SectionData("S2", ["C"]));
        flattenedAdapter.GetItemCount().Should().Be(3);

        // Add item to first section
        sections[0].Items.Add("D");
        flattenedAdapter.GetItemCount().Should().Be(4);

        // Remove item from second section
        sections[1].Items.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(3);

        // Add item back to second section
        sections[1].Items.Add("E");
        flattenedAdapter.GetItemCount().Should().Be(4);

        // Remove entire first section
        sections.RemoveAt(0);
        flattenedAdapter.GetItemCount().Should().Be(1);
        flattenedAdapter.GetItem(0).Value.Should().Be("E");

        // Clear remaining section's items
        sections[0].Items.Clear();
        flattenedAdapter.GetItemCount().Should().Be(0);

        // Add item to empty section
        sections[0].Items.Add("Final");
        flattenedAdapter.GetItemCount().Should().Be(1);
        flattenedAdapter.GetItem(0).Value.Should().Be("Final");
    }

    [Fact]
    public void GroupedCollection_WithEmptySections_ShouldHandleCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", []), // Empty section
            new("Section3", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert - Empty section should not contribute to count
        flattenedAdapter.GetItemCount().Should().Be(2);
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
    }

    [Fact]
    public void GroupedCollection_WithEmptySectionsAndHeaders_ShouldIncludeHeadersForEmptySections()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", []), // Empty section
            new("Section3", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert - Empty section should still have a header
        flattenedAdapter.GetItemCount().Should().Be(5, "3 section headers + 2 items");
        
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(4).Value.Should().Be("B");
    }

    #endregion

    #region Header/Footer Combination Tests - Single Collection

    [Fact]
    public void SingleCollection_WithGlobalHeaderOnly_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "global header + 2 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderOnly_WhenAddingItems_ShouldMaintainHeader()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.Add("B");
        collection.Add("C");

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "global header + 3 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterOnly_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "2 items + global footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterOnly_WhenRemovingItems_ShouldMaintainFooter()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.RemoveAt(1);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "2 items + global footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("C");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithSectionFooterOnly_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(3, "2 items + section footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithSectionFooterOnly_WhenAddingAndRemovingItems_ShouldMaintainFooter()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Add items
        collection.Add("B");
        collection.Add("C");

        // Assert after add
        flattenedAdapter.GetItemCount().Should().Be(4, "3 items + section footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);

        // Act - Remove item
        collection.RemoveAt(1);

        // Assert after remove
        flattenedAdapter.GetItemCount().Should().Be(3, "2 items + section footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("C");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndSectionHeader_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "global header + section header + 2 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndSectionFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "global header + 2 items + section footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterAndSectionHeader_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "section header + 2 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterAndSectionFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "2 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithAllCombinations_WhenClearingAndAdding_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Clear
        collection.Clear();
        var countAfterClear = flattenedAdapter.GetItemCount();

        // Add back
        collection.Add("X");
        collection.Add("Y");

        // Assert
        countAfterClear.Should().Be(2, "only global header and footer remain after clear");
        flattenedAdapter.GetItemCount().Should().Be(6, "all headers/footers + 2 items");
        
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("X");
        flattenedAdapter.GetItem(3).Value.Should().Be("Y");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithSectionHeaderAndFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "section header + 2 items + section footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithSectionHeaderAndFooter_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Add item
        collection.Add("C");

        // Assert after add
        flattenedAdapter.GetItemCount().Should().Be(5, "section header + 3 items + section footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);

        // Act - Replace item
        collection[1] = "X";

        // Assert after replace
        flattenedAdapter.GetItemCount().Should().Be(5);
        flattenedAdapter.GetItem(2).Value.Should().Be("X");

        // Act - Move item
        collection.Move(0, 2);

        // Assert after move
        flattenedAdapter.GetItem(1).Value.Should().Be("X");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Value.Should().Be("A");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeadersAndSectionHeader_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + section header + 2 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeadersAndSectionFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + 2 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndAllSection_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + section header + 2 items + section footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterAndAllSection_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "section header + 2 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeadersAndSectionHeader_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Add and remove items
        collection.Add("C");
        collection.RemoveAt(0);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + section header + 2 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeadersAndSectionFooter_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Replace and add items
        collection[0] = "X";
        collection.Add("C");

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "global header + 3 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("X");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndAllSection_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Move items
        collection.Move(2, 0);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "global header + section header + 3 items + section footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Value.Should().Be("A");
        flattenedAdapter.GetItem(4).Value.Should().Be("B");
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterAndAllSection_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Clear and add
        collection.Clear();
        collection.Add("X");
        collection.Add("Y");
        collection.Add("Z");

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "section header + 3 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("X");
        flattenedAdapter.GetItem(2).Value.Should().Be("Y");
        flattenedAdapter.GetItem(3).Value.Should().Be("Z");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndSectionHeader_WhenAddingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.Add("B");
        collection.Add("C");

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + section header + 3 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Value.Should().Be("C");
    }

    [Fact]
    public void SingleCollection_WithGlobalHeaderAndSectionFooter_WhenRemovingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.RemoveAt(1);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "global header + 2 items + section footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterAndSectionHeader_WhenReplacingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection[0] = "X";

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "section header + 2 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("X");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void SingleCollection_WithGlobalFooterAndSectionFooter_WhenMovingItems_ShouldMaintainStructure()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A", "B", "C" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        collection.Move(0, 2);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "3 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("B");
        flattenedAdapter.GetItem(1).Value.Should().Be("C");
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    #endregion

    #region Header/Footer Combination Tests - Grouped Collection

    [Fact]
    public void GroupedCollection_WithGlobalHeaderOnly_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "global header + 3 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
    }

    [Fact]
    public void GroupedCollection_WithGlobalFooterOnly_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "3 items + global footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithSectionFooterOnly_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "3 items + 2 section footers");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void GroupedCollection_WithSectionFooterOnly_WhenModifyingItems_ShouldMaintainFooters()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Add item to section
        sections[0].Items.Add("C");

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "3 items + section footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Value.Should().Be("C");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);

        // Act - Add new section
        sections.Add(new SectionData("Section2", ["X"]));

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "4 items + 2 section footers");
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeaderAndSectionHeader_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + 2 section headers + 2 items");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(4).Value.Should().Be("B");
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeaderAndSectionFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + 2 items + 2 section footers");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalFooterAndSectionHeader_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "2 section headers + 2 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalFooterAndSectionFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "2 items + 2 section footers + global footer");
        flattenedAdapter.GetItem(0).Value.Should().Be("A");
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithAllCombinations_WhenAddingAndRemovingSections_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Initial state
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + section header + 1 item + section footer + global footer");

        // Act - Add section
        sections.Add(new SectionData("Section2", ["B", "C"]));

        // Assert after add
        flattenedAdapter.GetItemCount().Should().Be(9, "global header + 2*(section header + items + section footer) + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(5).Value.Should().Be("B");
        flattenedAdapter.GetItem(6).Value.Should().Be("C");
        flattenedAdapter.GetItem(7).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(8).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);

        // Act - Remove first section
        sections.RemoveAt(0);

        // Assert after remove
        flattenedAdapter.GetItemCount().Should().Be(6, "global header + section header + 2 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithAllCombinations_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Initial: global header + section header + 2 items + section footer + global footer = 6
        flattenedAdapter.GetItemCount().Should().Be(6);

        // Act - Add item to section
        sections[0].Items.Add("C");

        // Assert after add
        flattenedAdapter.GetItemCount().Should().Be(7);
        flattenedAdapter.GetItem(4).Value.Should().Be("C");
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);

        // Act - Remove item from section
        sections[0].Items.RemoveAt(0);

        // Assert after remove
        flattenedAdapter.GetItemCount().Should().Be(6);
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);

        // Act - Replace item
        sections[0].Items[0] = "X";

        // Assert after replace
        flattenedAdapter.GetItemCount().Should().Be(6);
        flattenedAdapter.GetItem(2).Value.Should().Be("X");
    }

    [Fact]
    public void GroupedCollection_WithEmptySectionAndAllHeaders_ShouldIncludeHeadersAndFootersForEmptySection()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", []), // Empty section
            new("Section3", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        // global header + (S1 header + A + S1 footer) + (S2 header + S2 footer) + (S3 header + B + S3 footer) + global footer
        flattenedAdapter.GetItemCount().Should().Be(10);
        
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader); // Empty section header
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter); // Empty section footer
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(7).Value.Should().Be("B");
        flattenedAdapter.GetItem(8).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(9).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeaderAndFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + 3 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeaderAndFooter_WhenAddingSection_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        sections.Add(new SectionData("Section2", ["B", "C"]));

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "global header + 3 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Value.Should().Be("C");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeadersAndSectionHeader_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "global header + 2 section headers + 2 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(4).Value.Should().Be("B");
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeadersAndSectionFooter_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(6, "global header + 2 items + 2 section footers + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeaderAndAllSection_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(7, "global header + 2*(section header + item + section footer)");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(5).Value.Should().Be("B");
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalFooterAndAllSection_ShouldCountAndPositionCorrectly()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"]),
            new("Section2", ["B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(7, "2*(section header + item + section footer) + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(4).Value.Should().Be("B");
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeadersAndSectionHeader_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Add item and section
        sections[0].Items.Add("B");
        sections.Add(new SectionData("Section2", ["C"]));

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(7, "global header + 2 section headers + 3 items + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Value.Should().Be("B");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(5).Value.Should().Be("C");
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeadersAndSectionFooter_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Remove item
        sections[0].Items.RemoveAt(0);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(4, "global header + 1 item + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("B");
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalHeaderAndAllSection_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Add sections with items
        sections.Add(new SectionData("Section2", ["B", "C"]));

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(8, "global header + 2*(section header + items + section footer)");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(2).Value.Should().Be("A");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(5).Value.Should().Be("B");
        flattenedAdapter.GetItem(6).Value.Should().Be("C");
        flattenedAdapter.GetItem(7).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    public void GroupedCollection_WithGlobalFooterAndAllSection_WhenModifyingItems_ShouldMaintainStructure()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A", "B"]),
            new("Section2", ["C"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Remove section
        sections.RemoveAt(1);

        // Assert
        flattenedAdapter.GetItemCount().Should().Be(5, "section header + 2 items + section footer + global footer");
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(1).Value.Should().Be("A");
        flattenedAdapter.GetItem(2).Value.Should().Be("B");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    #endregion

    #region Change Notification Tests

    [Fact]
    public void SingleCollection_ShouldNotifySubscribersOnChanges()
    {
        // Arrange
        var collection = new ObservableCollection<string> { "A" };
        var adapter = new VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<string>>(collection);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        var notifications = new List<VirtualScrollFlattenedChangeSet>();
        using var subscription = flattenedAdapter.Subscribe(notifications.Add);

        // Act
        collection.Add("B");

        // Assert
        notifications.Should().HaveCount(1);
        notifications[0].Changes.Should().HaveCount(1);
        notifications[0].Changes.First().Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
    }

    [Fact]
    public void GroupedCollection_ShouldNotifySubscribersOnSectionChanges()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        var notifications = new List<VirtualScrollFlattenedChangeSet>();
        using var subscription = flattenedAdapter.Subscribe(notifications.Add);

        // Act
        sections.Add(new SectionData("Section2", ["B"]));

        // Assert
        notifications.Should().HaveCount(1);
        notifications[0].Changes.Should().HaveCount(1);
        notifications[0].Changes.First().Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
    }

    [Fact]
    public void GroupedCollection_ShouldNotifySubscribersOnItemChanges()
    {
        // Arrange
        var sections = new ObservableCollection<SectionData>
        {
            new("Section1", ["A"])
        };
        var adapter = new VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<SectionData>, ObservableCollection<string>>(
            sections,
            section => ((SectionData)section).Items);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        var notifications = new List<VirtualScrollFlattenedChangeSet>();
        using var subscription = flattenedAdapter.Subscribe(notifications.Add);

        // Act
        sections[0].Items.Add("B");

        // Assert
        notifications.Should().HaveCount(1);
        notifications[0].Changes.Should().HaveCount(1);
        notifications[0].Changes.First().Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
    }

    #endregion

    #region Helper Class

    private class SectionData
    {
        public string Name { get; set; }
        public ObservableCollection<string> Items { get; set; }

        public SectionData(string name, string[] items)
        {
            Name = name;
            Items = new ObservableCollection<string>(items);
        }
    }

    #endregion
}
