namespace Nalu.Maui.Test.Internals;

public class VirtualScrollFlattenedAdapterTests
{
    // Create adapter instance
    private static VirtualScrollFlattenedAdapter CreateAdapter(IVirtualScrollAdapter adapter, IVirtualScrollLayoutInfo layoutInfo) => new(adapter, layoutInfo);

    private IVirtualScrollAdapter CreateMockAdapter(int sectionCount, Func<int, int> getItemCount, Func<int, int, object?> getItem, Func<int, object?>? getSection = null)
    {
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => getItemCount(call.Arg<int>()));
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => getItem(call.ArgAt<int>(0), call.ArgAt<int>(1)));
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

    [Fact]
    internal void GetItemCountWhenAdapterIsEmptyShouldReturnZero()
    {
        // Arrange
        var adapter = CreateMockAdapter(0, _ => 0, (_, _) => null);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    internal void GetItemCountWithSingleSectionAndNoHeadersFootersShouldReturnItemCount()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    internal void GetItemCountWithGlobalHeaderAndFooterShouldIncludeThem()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 3, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(5); // 1 header + 3 items + 1 footer
    }

    [Fact]
    internal void GetItemCountWithSectionHeadersAndFootersShouldIncludeThem()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, _ => 3, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(10); // 2 sections * (1 header + 3 items + 1 footer)
    }

    [Fact]
    internal void GetItemCountWithAllHeadersAndFootersShouldIncludeAll()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var count = flattenedAdapter.GetItemCount();

        // Assert
        count.Should().Be(10); // 1 global header + 2 sections * (1 header + 2 items + 1 footer) + 1 global footer = 1 + 8 + 1 = 10
    }

    [Fact]
    internal void GetItemWithItemTypeShouldReturnCorrectItem()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 3, (s, i) => $"Section{s}Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(1);

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        item.Value.Should().Be("Section0Item1");
    }

    [Fact]
    internal void GetItemWithGlobalHeaderShouldReturnHeaderType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(0);

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        item.Value.Should().BeNull();
    }

    [Fact]
    internal void GetItemWithSectionHeaderShouldReturnSectionHeaderType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(0);

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        item.Value.Should().Be("Section0");
    }

    [Fact]
    internal void GetItemWithSectionFooterShouldReturnSectionFooterType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(2); // After 2 items

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
        item.Value.Should().Be("Section0");
    }

    [Fact]
    internal void GetItemWithGlobalFooterShouldReturnFooterType()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        var item = flattenedAdapter.GetItem(2); // After 2 items

        // Assert
        item.Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
        item.Value.Should().BeNull();
    }

    [Fact]
    internal void GetItemWithInvalidIndexShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act & Assert
        var action = () => flattenedAdapter.GetItem(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();

        action = () => flattenedAdapter.GetItem(10);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    internal void SubscribeWhenAdapterChangesShouldNotifySubscribers()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void SubscribeWhenDisposedShouldNotNotify()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var callCount = 0;
        var subscription = flattenedAdapter.Subscribe(_ => callCount++);
        subscription.Dispose();

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    internal void ChangeLayoutInfoWhenLayoutChangesShouldRebuildAndNotify()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo1 = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo1);
        
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

    [Fact]
    internal void ChangeLayoutInfoWhenLayoutDoesNotChangeShouldNotNotify()
    {
        // Arrange
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var callCount = 0;
        flattenedAdapter.Subscribe(_ => callCount++);

        // Act
        flattenedAdapter.ChangeLayoutInfo(layoutInfo);

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemShouldConvertToFlattenedChange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithRemoveItemShouldConvertToFlattenedChange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithReplaceItemShouldConvertToFlattenedChange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithMoveItemShouldConvertToFlattenedChange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithRefreshItemShouldConvertToFlattenedChange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithInsertItemRangeShouldConvertToFlattenedChange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithResetShouldConvertToFlattenedReset()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithInsertSectionShouldConvertToIncrementalInsert()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => call.Arg<int>() == 1 ? 3 : 2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionShouldConvertToIncrementalRemove()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionShouldConvertToRefreshRange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void GetItemWithMultipleSectionsShouldReturnCorrectItems()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, s => s == 0 ? 2 : 3, (s, i) => $"Section{s}Item{i}");
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act & Assert
        flattenedAdapter.GetItem(0).Value.Should().Be("Section0Item0");
        flattenedAdapter.GetItem(1).Value.Should().Be("Section0Item1");
        flattenedAdapter.GetItem(2).Value.Should().Be("Section1Item0");
        flattenedAdapter.GetItem(3).Value.Should().Be("Section1Item1");
        flattenedAdapter.GetItem(4).Value.Should().Be("Section1Item2");
    }

    [Fact]
    internal void GetItemWithSectionHeadersShouldCorrectlyIndexItems()
    {
        // Arrange
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"Section{s}Item{i}", s => $"SectionHeader{s}");
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

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

    [Fact]
    internal void DisposeShouldDisposeAdapterSubscription()
    {
        // Arrange
        var subscription = Substitute.For<IDisposable>();
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>()).Returns(subscription);
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Act
        flattenedAdapter.Dispose();

        // Assert
        subscription.Received(1).Dispose();
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeShouldConvertToIncrementalInsert()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeShouldConvertToIncrementalRemove()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
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

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionShouldConvertToReplaceItemRange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(1); // 2 items in section 0
    }

    [Fact]
    internal void OnAdapterChangedWithMoveSectionForwardShouldConvertToRemoveAndInsert()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section 0 to position 2 (forward)
        // Original: [Sec0(0-1), Sec1(2-3), Sec2(4-5)]
        // After: [Sec1(0-1), Sec2(2-3), Sec0(4-5)]
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(0);
        removeChange.EndItemIndex.Should().Be(1);
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(4);
        insertChange.EndItemIndex.Should().Be(5);
    }

    [Fact]
    internal void GetItemAfterInsertSectionShouldReturnCorrectItems()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => call.Arg<int>() == 1 ? 3 : 2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Section{call.ArgAt<int>(0)}Item{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

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

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithSectionHeadersFootersShouldIncludeHeadersFootersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 3;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers and footers
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 section header + 3 items + 1 footer = 5 items
        initialCount.Should().Be(5);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 1 header + 3 items + 1 footer = 5 items
        change.StartItemIndex.Should().Be(5); // After first section (5 items)
        change.EndItemIndex.Should().Be(9); // 5 + 5 - 1 = 9
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(10); // 5 + 5 = 10
        
        // Verify the inserted section structure
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(7).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(8).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(9).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithSectionHeadersFootersShouldIncludeHeadersFootersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers and footers
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 2 sections, each with 1 header + 2 items + 1 footer = 4 items per section, total 8
        initialCount.Should().Be(8);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 1 header + 2 items + 1 footer = 4 items
        change.StartItemIndex.Should().Be(0); // First section starts at 0
        change.EndItemIndex.Should().Be(3); // 0 + 4 - 1 = 3
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(4); // Only 1 section remaining: 1 header + 2 items + 1 footer = 4
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeWithSectionHeadersFootersShouldIncludeHeadersFootersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers and footers
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 section header + 2 items + 1 footer = 4 items
        initialCount.Should().Be(4);
        
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
        // Should insert: 2 sections * (1 header + 2 items + 1 footer) = 8 items
        change.StartItemIndex.Should().Be(4); // After first section (4 items)
        change.EndItemIndex.Should().Be(11); // 4 + 8 - 1 = 11
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(12); // 4 + 8 = 12
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWithSectionHeadersFootersShouldIncludeHeadersFootersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers and footers
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 3 sections, each with 1 header + 2 items + 1 footer = 4 items per section, total 12
        initialCount.Should().Be(12);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove 2 sections starting at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 sections * (1 header + 2 items + 1 footer) = 8 items
        change.StartItemIndex.Should().Be(0); // First section starts at 0
        change.EndItemIndex.Should().Be(7); // 0 + 8 - 1 = 7
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(4); // Only 1 section remaining: 1 header + 2 items + 1 footer = 4
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithSectionHeaderOnlyShouldIncludeHeaderInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 3;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers only (no footers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 section header + 3 items = 4 items
        initialCount.Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 1 header + 3 items = 4 items
        change.StartItemIndex.Should().Be(4); // After first section (4 items)
        change.EndItemIndex.Should().Be(7); // 4 + 4 - 1 = 7
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(8); // 4 + 4 = 8
        
        // Verify the inserted section structure
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionHeader);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(7).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithSectionFooterOnlyShouldIncludeFooterInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 3;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section footers only (no headers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: false, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 3 items + 1 footer = 4 items
        initialCount.Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 3 items + 1 footer = 4 items
        change.StartItemIndex.Should().Be(4); // After first section (4 items)
        change.EndItemIndex.Should().Be(7); // 4 + 4 - 1 = 7
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(8); // 4 + 4 = 8
        
        // Verify the inserted section structure
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(6).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(7).Type.Should().Be(VirtualScrollFlattenedPositionType.SectionFooter);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithSectionHeaderOnlyShouldIncludeHeaderInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers only (no footers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 2 sections, each with 1 header + 2 items = 3 items per section, total 6
        initialCount.Should().Be(6);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 1 header + 2 items = 3 items
        change.StartItemIndex.Should().Be(0); // First section starts at 0
        change.EndItemIndex.Should().Be(2); // 0 + 3 - 1 = 2
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // Only 1 section remaining: 1 header + 2 items = 3
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithSectionFooterOnlyShouldIncludeFooterInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section footers only (no headers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: false, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 2 sections, each with 2 items + 1 footer = 3 items per section, total 6
        initialCount.Should().Be(6);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 items + 1 footer = 3 items
        change.StartItemIndex.Should().Be(0); // First section starts at 0
        change.EndItemIndex.Should().Be(2); // 0 + 3 - 1 = 2
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // Only 1 section remaining: 2 items + 1 footer = 3
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeWithSectionHeaderOnlyShouldIncludeHeadersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers only (no footers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 section header + 2 items = 3 items
        initialCount.Should().Be(3);
        
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
        // Should insert: 2 sections * (1 header + 2 items) = 6 items
        change.StartItemIndex.Should().Be(3); // After first section (3 items)
        change.EndItemIndex.Should().Be(8); // 3 + 6 - 1 = 8
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(9); // 3 + 6 = 9
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeWithSectionFooterOnlyShouldIncludeFootersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section footers only (no headers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: false, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 2 items + 1 footer = 3 items
        initialCount.Should().Be(3);
        
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
        // Should insert: 2 sections * (2 items + 1 footer) = 6 items
        change.StartItemIndex.Should().Be(3); // After first section (3 items)
        change.EndItemIndex.Should().Be(8); // 3 + 6 - 1 = 8
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(9); // 3 + 6 = 9
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWithSectionHeaderOnlyShouldIncludeHeadersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section headers only (no footers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 3 sections, each with 1 header + 2 items = 3 items per section, total 9
        initialCount.Should().Be(9);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove 2 sections starting at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 sections * (1 header + 2 items) = 6 items
        change.StartItemIndex.Should().Be(0); // First section starts at 0
        change.EndItemIndex.Should().Be(5); // 0 + 6 - 1 = 5
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // Only 1 section remaining: 1 header + 2 items = 3
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWithSectionFooterOnlyShouldIncludeFootersInRange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with section footers only (no headers)
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: false, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 3 sections, each with 2 items + 1 footer = 3 items per section, total 9
        initialCount.Should().Be(9);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove 2 sections starting at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 sections * (2 items + 1 footer) = 6 items
        change.StartItemIndex.Should().Be(0); // First section starts at 0
        change.EndItemIndex.Should().Be(5); // 0 + 6 - 1 = 5
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // Only 1 section remaining: 2 items + 1 footer = 3
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithGlobalHeaderOnlyShouldAccountForGlobalHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global header only (no global footer)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 global header + 1 section (2 items) = 3 items
        initialCount.Should().Be(3);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 2 items (new section)
        // Start index should be after global header (1) + first section (2 items) = 3
        change.StartItemIndex.Should().Be(3);
        change.EndItemIndex.Should().Be(4); // 3 + 2 - 1 = 4
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(5); // 1 global header + 2 sections (2 items each) = 5
        
        // Verify structure: global header at 0, then sections
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithGlobalFooterOnlyShouldAccountForGlobalFooter()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global footer only (no global header)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: false, hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 section (2 items) + 1 global footer = 3 items
        initialCount.Should().Be(3);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 2 items (new section)
        // Start index should be after first section (2 items) = 2
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(3); // 2 + 2 - 1 = 3
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(5); // 2 sections (2 items each) + 1 global footer = 5
        
        // Verify structure: sections, then global footer at the end
        flattenedAdapter.GetItem(2).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithGlobalHeaderAndFooterShouldAccountForBoth()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with both global header and footer
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 global header + 1 section (2 items) + 1 global footer = 4 items
        initialCount.Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 2 items (new section)
        // Start index should be after global header (1) + first section (2 items) = 3
        change.StartItemIndex.Should().Be(3);
        change.EndItemIndex.Should().Be(4); // 3 + 2 - 1 = 4
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(6); // 1 global header + 2 sections (2 items each) + 1 global footer = 6
        
        // Verify structure
        flattenedAdapter.GetItem(0).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalHeader);
        flattenedAdapter.GetItem(3).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(4).Type.Should().Be(VirtualScrollFlattenedPositionType.Item);
        flattenedAdapter.GetItem(5).Type.Should().Be(VirtualScrollFlattenedPositionType.GlobalFooter);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithGlobalHeaderOnlyShouldAccountForGlobalHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global header only (no global footer)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 global header + 2 sections (2 items each) = 5 items
        initialCount.Should().Be(5);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 items (first section)
        // Start index should be after global header (1) = 1
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2); // 1 + 2 - 1 = 2
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // 1 global header + 1 section (2 items) = 3
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithGlobalFooterOnlyShouldAccountForGlobalFooter()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global footer only (no global header)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: false, hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 2 sections (2 items each) + 1 global footer = 5 items
        initialCount.Should().Be(5);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 items (first section)
        // Start index should be 0 (first section)
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(1); // 0 + 2 - 1 = 1
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // 1 section (2 items) + 1 global footer = 3
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeWithGlobalHeaderOnlyShouldAccountForGlobalHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global header only (no global footer)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 global header + 1 section (2 items) = 3 items
        initialCount.Should().Be(3);
        
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
        // Should insert: 2 sections * 2 items = 4 items
        // Start index should be after global header (1) + first section (2 items) = 3
        change.StartItemIndex.Should().Be(3);
        change.EndItemIndex.Should().Be(6); // 3 + 4 - 1 = 6
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(7); // 1 global header + 3 sections (2 items each) = 7
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeWithGlobalFooterOnlyShouldAccountForGlobalFooter()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global footer only (no global header)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: false, hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 section (2 items) + 1 global footer = 3 items
        initialCount.Should().Be(3);
        
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
        // Should insert: 2 sections * 2 items = 4 items
        // Start index should be after first section (2 items) = 2
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(5); // 2 + 4 - 1 = 5
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(7); // 3 sections (2 items each) + 1 global footer = 7
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWithGlobalHeaderOnlyShouldAccountForGlobalHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global header only (no global footer)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: false);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 1 global header + 3 sections (2 items each) = 7 items
        initialCount.Should().Be(7);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove 2 sections starting at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 sections * 2 items = 4 items
        // Start index should be after global header (1) = 1
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(4); // 1 + 4 - 1 = 4
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // 1 global header + 1 section (2 items) = 3
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWithGlobalFooterOnlyShouldAccountForGlobalFooter()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with global footer only (no global header)
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: false, hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        var initialCount = flattenedAdapter.GetItemCount();
        // Initial: 3 sections (2 items each) + 1 global footer = 7 items
        initialCount.Should().Be(7);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove 2 sections starting at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 sections * 2 items = 4 items
        // Start index should be 0 (first section)
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3); // 0 + 4 - 1 = 3
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(3); // 1 section (2 items) + 1 global footer = 3
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove items at indices 1 and 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemRangeShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace items at indices 1 and 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeShouldConvertToReplaceItemRange()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace sections 0 and 1 (4 items total at indices 0-3)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3); // 2 sections * 2 items = 4 items (indices 0-3)
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemCrossSectionShouldConvertToFlattenedChange()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 3, (s, i) => $"Section{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move item from section 0, index 1 to section 1, index 2
        // Note: This creates a custom change since MoveItem factory only supports same section
        var changeSet = new VirtualScrollChangeSet(new[] { new VirtualScrollChange(VirtualScrollChangeOperation.MoveItem, 0, 1, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        change.StartItemIndex.Should().Be(1); // Index 1 in section 0 (flattened: 1)
        change.EndItemIndex.Should().Be(5); // Index 2 in section 1 (flattened: 3 + 2 = 5)
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemWithSectionHeaderShouldAccountForHeader()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [Header0, Item0, Item1, Item2] - 4 items total
        flattenedAdapter.GetItemCount().Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // Flattened index should be 2 (after header at 0 and item0 at 1)
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemWithSectionHeaderShouldAccountForHeader()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        // Flattened index should be 2 (header at 0, item0 at 1, item1 at 2)
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [GlobalHeader, Item0, Item1, Item2] - 4 items total
        flattenedAdapter.GetItemCount().Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // Flattened index should be 2 (after global header at 0 and item0 at 1)
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemWithBothHeadersShouldAccountForBoth()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [GlobalHeader, SectionHeader0, Item0, Item1] - 4 items total
        flattenedAdapter.GetItemCount().Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 0 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // Flattened index should be 2 (global header at 0, section header at 1, new item at 2)
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemInSecondSectionShouldCalculateCorrectOffset()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, s => s == 0 ? 2 : 3, (s, i) => $"Section{s}Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [Header0, Item0-0, Item0-1, Header1, Item1-0, Item1-1, Item1-2] - 7 items total
        flattenedAdapter.GetItemCount().Should().Be(7);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 1 in section 1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(1, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // Section 0: Header(0) + 2 items(1,2) = 3 items
        // Section 1: Header(3) + Item1-0(4) + new item at index 1 = 5
        change.StartItemIndex.Should().Be(5);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove items at indices 1 and 2 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Header at 0, items start at 1, so removing indices 1-2 = flattened 2-3
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemWithSectionHeaderShouldAccountForHeader()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        // Header at 0, item0 at 1, item1 at 2
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 4, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move item from index 0 to index 3 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveItem(0, 0, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        // Header at 0, items at 1-4
        change.StartItemIndex.Should().Be(1); // item0 at flattened index 1
        change.EndItemIndex.Should().Be(4); // item3 at flattened index 4
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshItemWithSectionHeaderShouldAccountForHeader()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh item at index 2 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshItem(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        // Header at 0, item2 at flattened index 3
        change.StartItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemRangeWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 2 items at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Header at 0, item0 at 1, new items at 2-3
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemRangeWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace items at indices 1-3 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItemRange(0, 1, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        // Header at 0, items 1-3 at flattened 2-4
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(4);
    }

    // ─────────────────────────────
    // Item operations with SectionFooter
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithInsertItemWithSectionFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [Item0, Item1, Item2, Footer0] - 4 items total
        flattenedAdapter.GetItemCount().Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // Items start at 0, insert at index 1 = flattened index 1
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemWithSectionFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove item at index 2 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItem(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        change.StartItemIndex.Should().Be(2);
    }

    // ─────────────────────────────
    // Item operations with GlobalFooter
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithInsertItemWithGlobalFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [Item0, Item1, Item2, GlobalFooter] - 4 items total
        flattenedAdapter.GetItemCount().Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // Items start at 0, insert at index 1 = flattened index 1
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemWithGlobalFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        change.StartItemIndex.Should().Be(1);
    }

    // ─────────────────────────────
    // Item operations with ALL headers/footers
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithInsertItemWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial layout: [GlobalHeader, SectionHeader0, Item0, Item1, SectionFooter0, GlobalFooter] - 6 items total
        flattenedAdapter.GetItemCount().Should().Be(6);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        // GlobalHeader(0) + SectionHeader(1) + Item0(2) + new item at index 1 = 3
        change.StartItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemWithAllHeadersFootersShouldAccountForAll()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove item at index 2 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItem(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        // GlobalHeader(0) + SectionHeader(1) + Item0(2) + Item1(3) + Item2(4)
        change.StartItemIndex.Should().Be(4);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemWithAllHeadersFootersShouldAccountForAll()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace item at index 0 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItem(0, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        // GlobalHeader(0) + SectionHeader(1) + Item0(2)
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 4, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move item from index 0 to index 3 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveItem(0, 0, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        // GlobalHeader(0) + SectionHeader(1) + items at 2-5
        change.StartItemIndex.Should().Be(2); // item0 at flattened 2
        change.EndItemIndex.Should().Be(5); // item3 at flattened 5
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshItemWithAllHeadersFootersShouldAccountForAll()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        // GlobalHeader(0) + SectionHeader(1) + Item0(2) + Item1(3)
        change.StartItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemRangeWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 2 items at index 1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // GlobalHeader(0) + SectionHeader(1) + Item0(2) + new items at 3-4
        change.StartItemIndex.Should().Be(3);
        change.EndItemIndex.Should().Be(4);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove items at indices 1-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // GlobalHeader(0) + SectionHeader(1) + Item0(2) + items 1-2 at flattened 3-4
        change.StartItemIndex.Should().Be(3);
        change.EndItemIndex.Should().Be(4);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemRangeWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace items at indices 0-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        // GlobalHeader(0) + SectionHeader(1) + items 0-2 at flattened 2-4
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(4);
    }

    // ─────────────────────────────
    // Section operations with ALL headers/footers
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithInsertSectionWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: GlobalHeader + SectionHeader + 2 items + SectionFooter + GlobalFooter = 6
        flattenedAdapter.GetItemCount().Should().Be(6);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 1 section at index 1
        sectionCount = 2;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertSection(1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // Should insert: 1 header + 2 items + 1 footer = 4 items
        // Start after: GlobalHeader(1) + first section(4) = 5
        change.StartItemIndex.Should().Be(5);
        change.EndItemIndex.Should().Be(8); // 5 + 4 - 1 = 8
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(10); // 6 + 4 = 10
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: GlobalHeader + 2 sections * (SectionHeader + 2 items + SectionFooter) + GlobalFooter = 1 + 8 + 1 = 10
        flattenedAdapter.GetItemCount().Should().Be(10);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 1 header + 2 items + 1 footer = 4 items
        // Start at: GlobalHeader(1) -> section 0 starts at index 1
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(4); // 1 + 4 - 1 = 4
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(6); // 10 - 4 = 6
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionRangeWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 1;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: GlobalHeader + SectionHeader + 2 items + SectionFooter + GlobalFooter = 6
        flattenedAdapter.GetItemCount().Should().Be(6);
        
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
        // Should insert: 2 sections * (1 header + 2 items + 1 footer) = 8 items
        // Start after: GlobalHeader(1) + first section(4) = 5
        change.StartItemIndex.Should().Be(5);
        change.EndItemIndex.Should().Be(12); // 5 + 8 - 1 = 12
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(14); // 6 + 8 = 14
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(itemsPerSection);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: GlobalHeader + 3 sections * (SectionHeader + 2 items + SectionFooter) + GlobalFooter = 1 + 12 + 1 = 14
        flattenedAdapter.GetItemCount().Should().Be(14);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove 2 sections starting at index 0
        sectionCount = 1;
        
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Should remove: 2 sections * (1 header + 2 items + 1 footer) = 8 items
        // Start at: GlobalHeader(1) -> section 0 starts at index 1
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(8); // 1 + 8 - 1 = 8
        
        var newCount = flattenedAdapter.GetItemCount();
        newCount.Should().Be(6); // 14 - 8 = 6
    }

    // ─────────────────────────────
    // RefreshSection with different configurations
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionWithNoHeadersFootersShouldRefreshItems()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().NotBeEmpty();
        // With 3 items, should produce a ReplaceItemRange
        var change = receivedChangeSet.Changes.First();
        change.StartItemIndex.Should().Be(0);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionWithAllHeadersFootersShouldRefreshAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().NotBeEmpty();
        // Should start at index 1 (after global header)
        var change = receivedChangeSet.Changes.First();
        change.StartItemIndex.Should().Be(1);
    }

    // ─────────────────────────────
    // Item operations with GlobalHeader only
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithRemoveItemWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove item at index 1 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        // GlobalHeader(0) + Item0(1) + Item1(2)
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace item at index 0 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItem(0, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        // GlobalHeader(0) + Item0(1)
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move item from index 0 to index 3 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveItem(0, 0, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        // GlobalHeader(0) + items at 1-4
        change.StartItemIndex.Should().Be(1); // item0 at flattened 1
        change.EndItemIndex.Should().Be(4); // item3 at flattened 4
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshItemWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh item at index 2 in section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshItem(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        // GlobalHeader(0) + items, item2 at flattened index 3
        change.StartItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemRangeWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 2 items at index 1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.InsertItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        // GlobalHeader(0) + Item0(1) + new items at 2-3
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeWithGlobalHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove items at indices 1-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // GlobalHeader(0) + Item0(1) + items 1-2 at flattened 2-3
        change.StartItemIndex.Should().Be(2);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemRangeWithGlobalHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace items at indices 0-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        // GlobalHeader(0) + items 0-2 at flattened 1-3
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(3);
    }

    // ─────────────────────────────
    // ReplaceSection with different header/footer configurations
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace section 0 (1 header + 2 items = 3 items)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2); // Header + 2 items = 3 items (0-2)
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionFooterShouldAccountForFooter()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace section 0 (2 items + 1 footer = 3 items)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2); // 2 items + Footer = 3 items (0-2)
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithGlobalHeaderShouldAccountForHeader()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace section 0 (2 items)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(1); // After global header
        change.EndItemIndex.Should().Be(2); // 2 items (1-2)
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace section 0 (header + 2 items + footer = 4 items)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(1); // After global header
        change.EndItemIndex.Should().Be(4); // 4 items (1-4)
    }

    // ─────────────────────────────
    // ReplaceSectionRange with different header/footer configurations
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace sections 0 and 1 (2 * (1 header + 2 items) = 6 items)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(5); // 6 items (0-5)
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace sections 0 and 1 (2 * (1 header + 2 items + 1 footer) = 8 items)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(1); // After global header
        change.EndItemIndex.Should().Be(8); // 8 items (1-8)
    }

    // ─────────────────────────────
    // MoveSection with different header/footer configurations
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithMoveSectionBackwardShouldConvertToRemoveAndInsert()
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
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section 2 to position 0 (backward)
        // Original: [Sec0(0-1), Sec1(2-3), Sec2(4-5)]
        // After: [Sec2(0-1), Sec0(2-3), Sec1(4-5)]
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(2, 0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(4);
        removeChange.EndItemIndex.Should().Be(5);
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(0);
        insertChange.EndItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveSectionWithSectionHeaderShouldAccountForHeader()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Each section: 1 header + 2 items = 3 items
        // Total: 9 items [Header0, I0-0, I0-1, Header1, I1-0, I1-1, Header2, I2-0, I2-1]
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section 0 to position 2 (forward)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(0);
        removeChange.EndItemIndex.Should().Be(2); // 3 items (header + 2 items)
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(6); // After removing 3 items, insert at position 6
        insertChange.EndItemIndex.Should().Be(8);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveSectionWithAllHeadersFootersShouldAccountForAll()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // GlobalHeader + 3 sections * (1 header + 2 items + 1 footer) + GlobalFooter = 1 + 12 + 1 = 14 items
        flattenedAdapter.GetItemCount().Should().Be(14);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section 0 to position 2 (forward)
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(1); // After global header
        removeChange.EndItemIndex.Should().Be(4); // 4 items (header + 2 items + footer)
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(9); // After global header + 2 sections * 4 items = 1 + 8 = 9
        insertChange.EndItemIndex.Should().Be(12);
    }

    // ─────────────────────────────
    // Item operations with SectionFooter (remaining)
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithReplaceItemWithSectionFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace item at index 1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 4, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move item from index 0 to index 3
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveItem(0, 0, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshItemWithSectionFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh item at index 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshItem(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemRangeWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 2, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 2 items at index 1
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

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove items at indices 1-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemRangeWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace items at indices 0-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2);
    }

    // ─────────────────────────────
    // Item operations with GlobalFooter (remaining)
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithReplaceItemWithGlobalFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace item at index 1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItem(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        change.StartItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemWithGlobalFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move item from index 0 to index 3
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveItem(0, 0, 3) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.MoveItem);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshItemWithGlobalFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh item at index 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshItem(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        change.StartItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithInsertItemRangeWithGlobalFooterShouldWorkCorrectly()
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
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Insert 2 items at index 1
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

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeWithGlobalFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove items at indices 1-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemRangeWithGlobalFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 5, (_, i) => $"Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace items at indices 0-2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2);
    }

    // ─────────────────────────────
    // Section operations - remaining configs for full coverage
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithGlobalFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 3, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit ReplaceItemRange (structure unchanged, just content)
        // Layout: [S0I0, S0I1, S0I2, S1I0, S1I1, S1I2, GlobalFooter]
        // Section 0 has 3 items at indices 0-2
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace sections 0-1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit ReplaceItemRange (structure unchanged, just content)
        // Layout: [S0I0, S0I1, S0Footer, S1I0, S1I1, S1Footer, S2I0, S2I1, S2Footer]
        // Section 0-1 each have 2 items + 1 footer = 3 items each, total 6 items at indices 0-5
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(5);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWithGlobalHeaderShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace sections 0-1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit ReplaceItemRange (structure unchanged, just content)
        // Layout: [GlobalHeader, S0I0, S0I1, S1I0, S1I1, S2I0, S2I1]
        // Section 0-1 each have 2 items, total 4 items at indices 1-4 (offset by global header)
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(4);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWithGlobalFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Replace sections 0-1
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.ReplaceSectionRange(0, 1) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit ReplaceItemRange (structure unchanged, just content)
        // Layout: [S0I0, S0I1, S1I0, S1I1, S2I0, S2I1, GlobalFooter]
        // Section 0-1 each have 2 items, total 4 items at indices 0-3
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveSectionWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section from index 0 to index 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit RemoveItemRange followed by InsertItemRange
        // Layout: [S0I0, S0I1, S0Footer, S1I0, S1I1, S1Footer, S2I0, S2I1, S2Footer]
        // Section 0 has 2 items + 1 footer = 3 items at indices 0-2
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(0);
        removeChange.EndItemIndex.Should().Be(2);
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveSectionWithGlobalHeaderShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section from index 0 to index 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit RemoveItemRange followed by InsertItemRange
        // Layout: [GlobalHeader, S0I0, S0I1, S1I0, S1I1, S2I0, S2I1]
        // Section 0 has 2 items at indices 1-2 (offset by global header)
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(1);
        removeChange.EndItemIndex.Should().Be(2);
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
    }

    [Fact]
    internal void OnAdapterChangedWithMoveSectionWithGlobalFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Move section from index 0 to index 2
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.MoveSection(0, 2) });
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit RemoveItemRange followed by InsertItemRange
        // Layout: [S0I0, S0I1, S1I0, S1I1, S2I0, S2I1, GlobalFooter]
        // Section 0 has 2 items at indices 0-1
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var removeChange = receivedChangeSet.Changes.First();
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(0);
        removeChange.EndItemIndex.Should().Be(1);
        
        var insertChange = receivedChangeSet.Changes.Last();
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionWithSectionHeaderShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        // Layout: [S0Header, S0I0, S0I1, S1Header, S1I0, S1I1]
        // Section 0 has 1 header + 2 items = 3 items at indices 0-2
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionWithSectionFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        // Layout: [S0I0, S0I1, S0Footer, S1I0, S1I1, S1Footer]
        // Section 0 has 2 items + 1 footer = 3 items at indices 0-2
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionWithGlobalHeaderShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        // Layout: [GlobalHeader, S0I0, S0I1, S1I0, S1I1]
        // Section 0 has 2 items at indices 1-2 (offset by global header)
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(1);
        change.EndItemIndex.Should().Be(2);
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshSectionWithGlobalFooterShouldWorkCorrectly()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"S{s}Item{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Refresh section 0
        var changeSet = new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.RefreshSection(0) });
        adapterCallback?.Invoke(changeSet);

        // Assert
        // Layout: [S0I0, S0I1, S1I0, S1I1, GlobalFooter]
        // Section 0 has 2 items at indices 0-1
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(1);
    }
}

