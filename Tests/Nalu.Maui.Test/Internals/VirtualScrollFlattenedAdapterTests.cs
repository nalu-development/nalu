namespace Nalu.Maui.Test.Internals;

public class VirtualScrollFlattenedAdapterTests
{
    private IVirtualScrollAdapter CreateMockAdapter(int sectionCount, Func<int, int> getItemCount, Func<int, int, object?> getItem, Func<int, object?>? getSection = null)
    {
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => getItemCount(call.Arg<int>()));
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => getItem(call.Arg<int>(), call.Arg<int>()));
        if (getSection != null)
        {
            adapter.GetSection(Arg.Any<int>()).Returns(call => getSection(call.Arg<int>()));
        }
        else
        {
            adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        }
        return adapter;
    }

    private IVirtualScrollLayoutInfo CreateLayoutInfo(bool hasGlobalHeader = false, bool hasGlobalFooter = false, bool hasSectionHeader = false, bool hasSectionFooter = false)
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

    [Fact(DisplayName = "GetItemCount, when adapter is empty, should return 0")]
    public void GetItemCountWhenAdapterIsEmptyShouldReturnZero()
    {
        // Arrange
        var adapter = CreateMockAdapter(0, _ => 0, (_, _) => null);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact(DisplayName = "GetItemCount, with single section and no headers/footers, should return item count")]
    public void GetItemCountWithSingleSectionAndNoHeadersFootersShouldReturnItemCount()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(5);
    }

    [Fact(DisplayName = "GetItemCount, with global header and footer, should include them")]
    public void GetItemCountWithGlobalHeaderAndFooterShouldIncludeThem()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 3, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(5); // 1 header + 3 items + 1 footer
    }

    [Fact(DisplayName = "GetItemCount, with section headers and footers, should include them")]
    public void GetItemCountWithSectionHeadersAndFootersShouldIncludeThem()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, _ => 3, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(10); // 2 sections * (1 header + 3 items + 1 footer)
    }

    [Fact(DisplayName = "GetItemCount, with all headers and footers, should include all")]
    public void GetItemCountWithAllHeadersAndFootersShouldIncludeAll()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(11); // 1 global header + 2 sections * (1 header + 2 items + 1 footer) + 1 global footer
    }

    [Fact(DisplayName = "GetItem, with item type, should return correct item")]
    public void GetItemWithItemTypeShouldReturnCorrectItem()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 3, (s, i) => $"Section{s}Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(1);

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        item.Value.Should().Be("Section0Item1");
    }

    [Fact(DisplayName = "GetItem, with global header, should return header type")]
    public void GetItemWithGlobalHeaderShouldReturnHeaderType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(0);

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        item.Value.Should().BeNull();
    }

    [Fact(DisplayName = "GetItem, with section header, should return section header type")]
    public void GetItemWithSectionHeaderShouldReturnSectionHeaderType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(0);

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        item.Value.Should().Be("Section0");
    }

    [Fact(DisplayName = "GetItem, with section footer, should return section footer type")]
    public void GetItemWithSectionFooterShouldReturnSectionFooterType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(2); // After 2 items

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        item.Value.Should().Be("Section0");
    }

    [Fact(DisplayName = "GetItem, with global footer, should return footer type")]
    public void GetItemWithGlobalFooterShouldReturnFooterType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(2); // After 2 items

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
        item.Value.Should().BeNull();
    }

    [Fact(DisplayName = "GetItem, with invalid index, should throw ArgumentOutOfRangeException")]
    public void GetItemWithInvalidIndexShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        var action = () => flattenedAdapter.GetItem(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();

        action = () => flattenedAdapter.GetItem(10);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Subscribe, when adapter changes, should notify subscribers")]
    public void SubscribeWhenAdapterChangesShouldNotifySubscribers()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        var subscription = flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        receivedChangeSet.Changes.First().Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);

        subscription.Dispose();
    }

    [Fact(DisplayName = "Subscribe, when disposed, should not notify")]
    public void SubscribeWhenDisposedShouldNotNotify()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        var callCount = 0;
        var subscription = flattenedAdapter.Subscribe(_ => callCount++);
        subscription.Dispose();

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        callCount.Should().Be(0);
    }

    [Fact(DisplayName = "ChangeLayoutInfo, when layout changes, should rebuild and notify")]
    public void ChangeLayoutInfoWhenLayoutChangesShouldRebuildAndNotify()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo1 = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo1);
        
        var initialCount = flattenedAdapter.GetItemCount();
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var layoutInfo2 = CreateLayoutInfo(hasGlobalHeader: true);
        flattenedAdapter.ChangeLayoutInfo(layoutInfo2);

        // Assert
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(initialCount + 1); // Added global header
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        receivedChangeSet.Changes.First().Operation.Should().Be(VirtualScrollFlattenedChangeOperation.Reset);
    }

    [Fact(DisplayName = "ChangeLayoutInfo, when layout does not change, should not notify")]
    public void ChangeLayoutInfoWhenLayoutDoesNotChangeShouldNotNotify()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        var callCount = 0;
        flattenedAdapter.Subscribe(_ => callCount++);

        // Act
        flattenedAdapter.ChangeLayoutInfo(layoutInfo);

        // Assert
        callCount.Should().Be(0);
    }

    [Fact(DisplayName = "OnAdapterChanged, with InsertItem, should convert to flattened change")]
    public void OnAdapterChangedWithInsertItemShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        change.StartItemIndex.Should().Be(1); // Inserted at item index 1 in section 0
    }

    [Fact(DisplayName = "OnAdapterChanged, with RemoveItem, should convert to flattened change")]
    public void OnAdapterChangedWithRemoveItemShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 3, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact(DisplayName = "OnAdapterChanged, with ReplaceItem, should convert to flattened change")]
    public void OnAdapterChangedWithReplaceItemShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 3, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact(DisplayName = "OnAdapterChanged, with MoveItem, should convert to flattened change")]
    public void OnAdapterChangedWithMoveItemShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 4, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveItem(0, 1, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact(DisplayName = "OnAdapterChanged, with RefreshItem, should convert to flattened change")]
    public void OnAdapterChangedWithRefreshItemShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 3, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact(DisplayName = "OnAdapterChanged, with InsertItemRange, should convert to flattened change")]
    public void OnAdapterChangedWithInsertItemRangeShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact(DisplayName = "OnAdapterChanged, with Reset, should convert to flattened reset")]
    public void OnAdapterChangedWithResetShouldConvertToFlattenedReset()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.Reset() });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.Reset);
    }

    [Fact(DisplayName = "OnAdapterChanged, with InsertSection, should convert to incremental insert")]
    public void OnAdapterChangedWithInsertSectionShouldConvertToIncrementalInsert()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => call.Arg<int>() == 1 ? 3 : 2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.Arg<int>()}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Update adapter to have 2 sections after insert
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should have inserted the new section's items
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().BeGreaterThan(initialCount);
    }

    [Fact(DisplayName = "OnAdapterChanged, with RemoveSection, should convert to incremental remove")]
    public void OnAdapterChangedWithRemoveSectionShouldConvertToIncrementalRemove()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.Arg<int>()}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Update adapter to have 1 section after remove
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should have removed the section's items
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().BeLessThan(initialCount);
    }

    [Fact(DisplayName = "OnAdapterChanged, with RefreshSection, should convert to refresh range")]
    public void OnAdapterChangedWithRefreshSectionShouldConvertToRefreshRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 3, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        // Should have refresh for section header and items
        receivedChangeSet!.Changes.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "GetItem, with multiple sections, should return correct items")]
    public void GetItemWithMultipleSectionsShouldReturnCorrectItems()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, s => s == 0 ? 2 : 3, (s, i) => $"Section{s}Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItem(0).Value.Should().Be("Section0Item0");
        flattenedAdapter.GetItem(1).Value.Should().Be("Section0Item1");
        flattenedAdapter.GetItem(2).Value.Should().Be("Section1Item0");
        flattenedAdapter.GetItem(3).Value.Should().Be("Section1Item1");
        flattenedAdapter.GetItem(4).Value.Should().Be("Section1Item2");
    }

    [Fact(DisplayName = "GetItem, with section headers, should correctly index items")]
    public void GetItemWithSectionHeadersShouldCorrectlyIndexItems()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"Section{s}Item{i}", s => $"SectionHeader{s}");
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(0).Value.Should().Be("SectionHeader0");
        flattenedAdapter.GetItem(1).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(1).Value.Should().Be("Section0Item0");
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(3).Value.Should().Be("SectionHeader1");
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(4).Value.Should().Be("Section1Item0");
    }

    [Fact(DisplayName = "Dispose, should dispose adapter subscription")]
    public void DisposeShouldDisposeAdapterSubscription()
    {
        // Arrange
        var subscription = Substitute.For<IDisposable>();
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>()).Returns(subscription);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act
        flattenedAdapter.Dispose();

        // Assert
        subscription.Received(1).Dispose();
    }

    [Fact(DisplayName = "OnAdapterChanged, with InsertSectionRange, should convert to incremental insert")]
    public void OnAdapterChangedWithInsertSectionRangeShouldConvertToIncrementalInsert()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.Arg<int>()}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 2 sections at index 1
        sectionCount = 3;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSectionRange(1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().BeGreaterThan(initialCount);
    }

    [Fact(DisplayName = "OnAdapterChanged, with RemoveSectionRange, should convert to incremental remove")]
    public void OnAdapterChangedWithRemoveSectionRangeShouldConvertToIncrementalRemove()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.Arg<int>()}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove sections 0 and 1
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().BeLessThan(initialCount);
    }

    [Fact(DisplayName = "OnAdapterChanged, with ReplaceSection, should convert to reset")]
    public void OnAdapterChangedWithReplaceSectionShouldConvertToReset()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.Reset);
    }

    [Fact(DisplayName = "OnAdapterChanged, with MoveSection, should convert to reset")]
    public void OnAdapterChangedWithMoveSectionShouldConvertToReset()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.Reset);
    }

    [Fact(DisplayName = "GetItem, after InsertSection, should return correct items")]
    public void GetItemAfterInsertSectionShouldReturnCorrectItems()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => call.Arg<int>() == 1 ? 3 : 2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}Item{call.Arg<int>()}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);

        // Act - Insert a section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Verify items are correctly positioned
        flattenedAdapter.GetItem(0).Value.Should().Be("Section0Item0");
        flattenedAdapter.GetItem(1).Value.Should().Be("Section0Item1");
        flattenedAdapter.GetItem(2).Value.Should().Be("Section1Item0");
        flattenedAdapter.GetItem(3).Value.Should().Be("Section1Item1");
        flattenedAdapter.GetItem(4).Value.Should().Be("Section1Item2");
    }
}

