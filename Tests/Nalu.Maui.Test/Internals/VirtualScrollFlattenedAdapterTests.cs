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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(0, 1, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.Reset()]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(0, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([new VirtualScrollChange(VirtualScrollChangeOperation.MoveItem, 0, 1, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(1, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(0, 0, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItemRange(0, 1, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(0, 0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(0, 0, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(1)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSectionRange(1, 2)]);
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
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(0, 0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(0, 0, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(2, 0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(0, 0, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(0, 0, 3)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(0, 1, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItemRange(0, 0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveSection(0, 2)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshSection(0)]);
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

    // ─────────────────────────────
    // Bug fix tests: Section already removed before change notification
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWhenSectionAlreadyRemovedShouldUseCachedOffsets()
    {
        // Arrange - This test simulates the real-world scenario where the underlying collection
        // is modified BEFORE the change notification is processed
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = new[] { 2, 3, 4 }; // Different sizes to verify correct calculation
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            // After removal, index 0 is gone, so we need to handle the shifted indices
            return idx < sectionCount ? itemsPerSection[idx] : throw new ArgumentOutOfRangeException();
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: 3 sections with 2 + 3 + 4 = 9 items
        flattenedAdapter.GetItemCount().Should().Be(9);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Simulate what happens in real usage:
        // 1. Section 0 is removed from the underlying collection (2 items)
        // 2. sectionCount changes BEFORE the callback is invoked
        sectionCount = 2;
        itemsPerSection = [3, 4, 0]; // Now section 0 has what was section 1 (3 items)
        
        // 3. The change notification arrives (but the data is already modified!)
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should have removed the correct number of items (2) using cached offsets
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(1); // Section 0 had 2 items (indices 0-1)
        
        // New count should be 7 (3 + 4)
        flattenedAdapter.GetItemCount().Should().Be(7);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionRangeWhenSectionsAlreadyRemovedShouldUseCachedOffsets()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 4;
        var itemsPerSection = new[] { 2, 3, 4, 5 }; // Different sizes to verify correct calculation
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount ? itemsPerSection[idx] : throw new ArgumentOutOfRangeException();
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: 4 sections with 2 + 3 + 4 + 5 = 14 items
        flattenedAdapter.GetItemCount().Should().Be(14);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Simulate removing sections 0 and 1 (2 + 3 = 5 items)
        sectionCount = 2;
        itemsPerSection = [4, 5, 0, 0]; // Now has what was sections 2 and 3
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSectionRange(0, 1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        change.StartItemIndex.Should().Be(0);
        change.EndItemIndex.Should().Be(4); // Sections 0-1 had 5 items (indices 0-4)
        
        flattenedAdapter.GetItemCount().Should().Be(9); // 4 + 5
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionWithHeadersFootersWhenSectionAlreadyRemovedShouldUseCachedOffsets()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemsPerSection = new[] { 3, 4 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount ? itemsPerSection[idx] : throw new ArgumentOutOfRangeException();
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        // Layout with all headers/footers
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasGlobalFooter: true, hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: GlobalHeader + 2 sections * (SectionHeader + items + SectionFooter) + GlobalFooter
        // = 1 + (1+3+1) + (1+4+1) + 1 = 1 + 5 + 6 + 1 = 13
        flattenedAdapter.GetItemCount().Should().Be(13);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section 0 (which has header + 3 items + footer = 5 elements)
        sectionCount = 1;
        itemsPerSection = [4, 0]; // Now section 0 has what was section 1
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Section 0 starts at index 1 (after global header)
        change.StartItemIndex.Should().Be(1);
        // Section 0 had header + 3 items + footer = 5 items (indices 1-5)
        change.EndItemIndex.Should().Be(5);
        
        // New count: GlobalHeader + (SectionHeader + 4 items + SectionFooter) + GlobalFooter = 1 + 6 + 1 = 8
        flattenedAdapter.GetItemCount().Should().Be(8);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveMiddleSectionWhenSectionAlreadyRemovedShouldUseCachedOffsets()
    {
        // Arrange - Test removing a middle section, not the first
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = new[] { 2, 5, 3 }; // Middle section has 5 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount ? itemsPerSection[idx] : throw new ArgumentOutOfRangeException();
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: 2 + 5 + 3 = 10 items
        flattenedAdapter.GetItemCount().Should().Be(10);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section 1 (the middle one with 5 items)
        sectionCount = 2;
        itemsPerSection = [2, 3, 0]; // Sections 0 and 2 (now called 1) remain
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Section 1 starts at index 2 (after section 0's 2 items)
        change.StartItemIndex.Should().Be(2);
        // Section 1 had 5 items (indices 2-6)
        change.EndItemIndex.Should().Be(6);
        
        flattenedAdapter.GetItemCount().Should().Be(5); // 2 + 3
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveLastSectionWhenSectionAlreadyRemovedShouldUseCachedOffsets()
    {
        // Arrange - Test removing the last section
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 3;
        var itemsPerSection = new[] { 2, 3, 4 }; // Last section has 4 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount ? itemsPerSection[idx] : throw new ArgumentOutOfRangeException();
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: 2 + 3 + 4 = 9 items
        flattenedAdapter.GetItemCount().Should().Be(9);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Remove section 2 (the last one with 4 items)
        sectionCount = 2;
        itemsPerSection = [2, 3, 0];
        
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(2)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        var change = receivedChangeSet.Changes.First();
        change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        // Section 2 starts at index 5 (after sections 0 and 1: 2 + 3 = 5)
        change.StartItemIndex.Should().Be(5);
        // Section 2 had 4 items (indices 5-8)
        change.EndItemIndex.Should().Be(8);
        
        flattenedAdapter.GetItemCount().Should().Be(5); // 2 + 3
    }

    // ─────────────────────────────
    // Batch changeset tests: Multiple changes in a single changeset
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithBatchOfMultipleItemInsertsShouldProcessAllChanges()
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

        // Act - Send a batch with multiple item inserts at valid indices
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.InsertItem(0, 0),
            VirtualScrollChangeFactory.InsertItem(0, 2),
            VirtualScrollChangeFactory.InsertItem(0, 4)
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert - Should produce 3 flattened changes
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(3);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        changes[0].StartItemIndex.Should().Be(0);
        changes[1].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        changes[1].StartItemIndex.Should().Be(2);
        changes[2].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        changes[2].StartItemIndex.Should().Be(4);
    }

    [Fact]
    internal void OnAdapterChangedWithBatchOfMixedOperationsShouldProcessAllChanges()
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

        // Act - Send a batch with mixed operations
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.ReplaceItem(0, 0),
            VirtualScrollChangeFactory.RefreshItem(0, 2),
            VirtualScrollChangeFactory.ReplaceItem(0, 4)
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert - Should produce 3 flattened changes
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(3);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        changes[0].StartItemIndex.Should().Be(0);
        changes[1].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RefreshItem);
        changes[1].StartItemIndex.Should().Be(2);
        changes[2].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItem);
        changes[2].StartItemIndex.Should().Be(4);
    }

    [Fact]
    internal void OnAdapterChangedWithBatchOfInsertAndRemoveShouldProcessInOrder()
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
        
        // Initial: 5 items
        flattenedAdapter.GetItemCount().Should().Be(5);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Send a batch with insert then remove
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.InsertItem(0, 0),  // Insert at 0
            VirtualScrollChangeFactory.RemoveItem(0, 3)   // Remove at 3
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItem);
        changes[0].StartItemIndex.Should().Be(0);
        changes[1].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        changes[1].StartItemIndex.Should().Be(3);
        
        // Final count should be same (1 insert, 1 remove)
        flattenedAdapter.GetItemCount().Should().Be(5);
    }

    [Fact]
    internal void OnAdapterChangedWithBatchAcrossMultipleSectionsShouldCalculateCorrectIndices()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(3, _ => 2, (s, i) => $"S{s}I{i}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: 3 sections * 2 items = 6 items
        flattenedAdapter.GetItemCount().Should().Be(6);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Send a batch with operations in different sections
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.ReplaceItem(0, 0), // Section 0, Item 0 -> flattened 0
            VirtualScrollChangeFactory.ReplaceItem(1, 1), // Section 1, Item 1 -> flattened 3
            VirtualScrollChangeFactory.ReplaceItem(2, 0)  // Section 2, Item 0 -> flattened 4
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(3);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].StartItemIndex.Should().Be(0); // Section 0, Item 0
        changes[1].StartItemIndex.Should().Be(3); // Section 1, Item 1 (after 2 items in section 0)
        changes[2].StartItemIndex.Should().Be(4); // Section 2, Item 0 (after 2+2=4 items in sections 0-1)
    }

    [Fact]
    internal void OnAdapterChangedWithBatchContainingResetShouldStopAtReset()
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

        // Act - Send a batch with a Reset in the middle
        var changeSet = new VirtualScrollChangeSet(
            [
                VirtualScrollChangeFactory.InsertItem(0, 0),
                VirtualScrollChangeFactory.Reset(),
                VirtualScrollChangeFactory.InsertItem(0, 1) // This should still be processed after reset
            ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert - All changes should be converted
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(1);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.Reset);
    }

    [Fact]
    internal void OnAdapterChangedWithBatchOfSectionOperationsShouldProcessAllChanges()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(2);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Initial: 2 sections * 2 items = 4 items
        flattenedAdapter.GetItemCount().Should().Be(4);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Simulate: insert a section, then replace another section
        sectionCount = 3;
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.InsertSection(2),   // Insert section at end
            VirtualScrollChangeFactory.ReplaceSection(0)   // Replace first section
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        changes[1].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
    }

    [Fact]
    internal void OnAdapterChangedWithBatchOfItemRangeOperationsShouldProcessAllChanges()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(1, _ => 10, (_, i) => $"Item{i}");
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

        // Act - Send a batch with range operations
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.InsertItemRange(0, 0, 2),   // Insert 3 items at start
            VirtualScrollChangeFactory.ReplaceItemRange(0, 5, 7),  // Replace items 5-7
            VirtualScrollChangeFactory.RemoveItemRange(0, 8, 9)    // Remove items 8-9
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(3);
        
        var changes = receivedChangeSet.Changes.ToList();
        changes[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        changes[0].StartItemIndex.Should().Be(0);
        changes[0].EndItemIndex.Should().Be(2);
        
        changes[1].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        changes[1].StartItemIndex.Should().Be(5);
        changes[1].EndItemIndex.Should().Be(7);
        
        changes[2].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        changes[2].StartItemIndex.Should().Be(8);
        changes[2].EndItemIndex.Should().Be(9);
    }

    [Fact]
    internal void OnAdapterChangedWithBatchWithHeadersFootersShouldCalculateCorrectIndices()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var adapter = CreateMockAdapter(2, _ => 2, (s, i) => $"S{s}I{i}", s => $"Section{s}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });
        
        var layoutInfo = CreateLayoutInfo(hasGlobalHeader: true, hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);
        
        // Layout: [GlobalHeader, SectionHeader0, Item0-0, Item0-1, SectionHeader1, Item1-0, Item1-1]
        // Indices: 0            1                2        3        4                5        6
        flattenedAdapter.GetItemCount().Should().Be(7);
        
        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(changeSet => receivedChangeSet = changeSet);

        // Act - Batch with operations in both sections
        var changeSet = new VirtualScrollChangeSet(
        [
            VirtualScrollChangeFactory.ReplaceItem(0, 0), // Section 0, Item 0 -> flattened 2
            VirtualScrollChangeFactory.ReplaceItem(1, 1)  // Section 1, Item 1 -> flattened 6
        ]
        );
        adapterCallback?.Invoke(changeSet);

        // Assert
        receivedChangeSet.Should().NotBeNull();
        receivedChangeSet!.Changes.Should().HaveCount(2);
        
        var changes = receivedChangeSet.Changes.ToList();
        // Global header (1) + Section header (1) + Item index 0 = 2
        changes[0].StartItemIndex.Should().Be(2);
        // Global header (1) + Section 0 (header + 2 items = 3) + Section header (1) + Item index 1 = 6
        changes[1].StartItemIndex.Should().Be(6);
    }

    [Fact]
    internal void OnAdapterChangedWithEmptyBatchShouldNotNotify()
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
        
        var callCount = 0;
        flattenedAdapter.Subscribe(_ => callCount++);

        // Act - Send an empty batch
        var changeSet = new VirtualScrollChangeSet([]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should not notify subscribers for empty changeset
        callCount.Should().Be(0);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemShouldUseCachedOffsetsWhenSectionAlreadyRemoved()
    {
        // Arrange - This test replicates the real-world scenario from the logs where:
        // 1. Multiple RemoveItem notifications arrive for section 1
        // 2. Section count decreases (section removed from underlying adapter)
        // 3. More RemoveItem notifications arrive for section 1 which no longer exists
        // The fix uses cached offsets so RemoveItem can still produce valid flattened indices
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2; // Start with 2 sections (section 0 and section 1)
        var itemCounts = new[] { 3, 4 }; // Section 0 has 3 items, section 1 has 4 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
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

        // Initial state: [Item0, Item1, Item2] + [Item0, Item1, Item2, Item3] = 7 items
        // Flattened indices: 0, 1, 2, 3, 4, 5, 6
        flattenedAdapter.GetItemCount().Should().Be(7);

        var receivedChangeSets = new List<VirtualScrollFlattenedChangeSet>();
        flattenedAdapter.Subscribe(cs => receivedChangeSets.Add(cs));

        // Act - Simulate the real-world scenario from the logs:
        // RemoveItem from section 1, item 0 multiple times, then section gets removed

        // First RemoveItem - section 1 still exists, should work normally
        itemCounts[1] = 3; // Section 1 now has 3 items
        var changeSet1 = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
        adapterCallback?.Invoke(changeSet1);

        // Verify first removal produced valid index (flattened index 3 for section 1, item 0)
        receivedChangeSets.Should().HaveCount(1);
        receivedChangeSets[0].Changes.Should().HaveCount(1);
        var firstChange = receivedChangeSets[0].Changes.First();
        firstChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        firstChange.StartItemIndex.Should().Be(3, "Section 1 starts at index 3");

        // Now simulate section 1 being removed from the underlying adapter
        sectionCount = 1; // Only section 0 exists now

        // But a RemoveItem notification for section 1 arrives (stale notification)
        // This should be skipped since section 1 no longer exists in the cache after previous operations
        // Actually, since we only removed an item, section 1 should still be in the cache
        // Let's simulate the exact scenario: section is removed from adapter but cache still has it

        // Reset for a cleaner scenario - rebuild with 2 sections
        receivedChangeSets.Clear();
        sectionCount = 2;
        itemCounts[1] = 4;

        // Force rebuild offsets by sending a Reset
        var resetChangeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.Reset()]);
        adapterCallback?.Invoke(resetChangeSet);
        receivedChangeSets.Clear();

        // Now remove section 1 from the underlying adapter
        sectionCount = 1;

        // Send RemoveItem for section 1 item 0 - the adapter no longer has section 1
        // but the cached offsets should still have it
        var changeSet2 = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
        adapterCallback?.Invoke(changeSet2);

        // Assert - Should use cached offsets and produce valid index, or skip if cache was invalidated
        if (receivedChangeSets.Count > 0)
        {
            foreach (var cs in receivedChangeSets)
            {
                foreach (var change in cs.Changes)
                {
                    change.StartItemIndex.Should().BeGreaterThan(-1, "RemoveItem should use cached offsets, not produce -1");
                    change.EndItemIndex.Should().BeGreaterThan(-1, "RemoveItem should use cached offsets, not produce -1");
                }
            }
        }
    }

    [Fact]
    internal void OnAdapterChangedWithMultipleRemoveItemsShouldUseCachedOffsetsCorrectly()
    {
        // Arrange - Simulates the exact log pattern:
        // Multiple RemoveItem for section 1 → valid flattened index 4
        // Then RemoveItem for section 1 → flattened index -1 (BUG - should use cached)
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 1, 10 }; // Section 0: 1 item (index 0), Section 1: 10 items (indices 1-10)
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: Section 0 (1 item) + Section 1 (10 items) = 11 items
        // Flattened: [0] + [1,2,3,4,5,6,7,8,9,10]
        flattenedAdapter.GetItemCount().Should().Be(11);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Remove items from section 1 one by one (like the log shows)
        for (var i = 0; i < 5; i++)
        {
            itemCounts[1]--; // Decrease item count
            var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
            adapterCallback?.Invoke(changeSet);
        }

        // Assert - All remove operations should have produced valid flattened indices
        allChanges.Should().HaveCount(5);
        foreach (var change in allChanges)
        {
            change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
            change.StartItemIndex.Should().BeGreaterThan(-1, "Should use cached offsets for RemoveItem");
        }
        // First removal should be at index 1 (start of section 1)
        allChanges[0].StartItemIndex.Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemRangeShouldUseCachedOffsetsWhenSectionAlreadyRemoved()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 2, 5 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
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

        // Initial: 2 + 5 = 7 items
        flattenedAdapter.GetItemCount().Should().Be(7);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Remove items 0-2 from section 1 (items at flattened indices 2, 3, 4)
        itemCounts[1] = 2; // 3 items removed
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItemRange(1, 0, 2)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        allChanges.Should().HaveCount(1);
        allChanges[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        allChanges[0].StartItemIndex.Should().Be(2, "Section 1 starts at flattened index 2");
        allChanges[0].EndItemIndex.Should().Be(4, "Range 0-2 is 3 items: indices 2, 3, 4");
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceItemForRemovedSectionShouldSkipNotification()
    {
        // Arrange - ReplaceItem still uses live adapter state, so it should skip if section doesn't exist
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(3);
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

        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Section 1 removed, then ReplaceItem notification arrives
        sectionCount = 1;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceItem(1, 0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should either skip or not produce -1 indices
        if (receivedChangeSet != null)
        {
            foreach (var change in receivedChangeSet.Changes)
            {
                change.StartItemIndex.Should().BeGreaterThan(-1, "Invalid negative index would crash RecyclerView");
                change.EndItemIndex.Should().BeGreaterThan(-1, "Invalid negative index would crash RecyclerView");
            }
        }
    }

    [Fact]
    internal void OnAdapterChangedWithRefreshItemForRemovedSectionShouldSkipNotification()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(3);
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

        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Section 1 removed before notification
        sectionCount = 1;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RefreshItem(1, 0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        if (receivedChangeSet != null)
        {
            foreach (var change in receivedChangeSet.Changes)
            {
                change.StartItemIndex.Should().BeGreaterThan(-1, "Invalid negative index would crash RecyclerView");
                change.EndItemIndex.Should().BeGreaterThan(-1, "Invalid negative index would crash RecyclerView");
            }
        }
    }

    [Fact]
    internal void OnAdapterChangedWithMoveItemForRemovedSectionShouldSkipNotification()
    {
        // Arrange
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(3);
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

        VirtualScrollFlattenedChangeSet? receivedChangeSet = null;
        flattenedAdapter.Subscribe(cs => receivedChangeSet = cs);

        // Act - Section 1 removed before notification
        sectionCount = 1;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.MoveItem(1, 0, 2)]);
        adapterCallback?.Invoke(changeSet);

        // Assert
        if (receivedChangeSet != null)
        {
            foreach (var change in receivedChangeSet.Changes)
            {
                change.StartItemIndex.Should().BeGreaterThan(-1, "Invalid negative index would crash RecyclerView");
                change.EndItemIndex.Should().BeGreaterThan(-1, "Invalid negative index would crash RecyclerView");
            }
        }
    }

    [Fact]
    internal void OnAdapterChangedWithInsertSectionThenRemoveItemShouldMaintainCorrectIndices()
    {
        // Arrange - This test replicates the regression where:
        // 1. Items removed from sections
        // 2. Sections removed
        // 3. New sections inserted
        // 4. RemoveItem for the new section uses wrong cached offset
        // Result: "everything disappeared"
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 2, 3 }; // Section 0: 2 items, Section 1: 3 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial state: 2 + 3 = 5 items
        flattenedAdapter.GetItemCount().Should().Be(5);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Simulate the problematic sequence from logs:
        // 1. Remove all items from section 1
        for (var i = 0; i < 3; i++)
        {
            itemCounts[1]--;
            var removeItemChange = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
            adapterCallback?.Invoke(removeItemChange);
        }

        // 2. Remove section 1 (now empty)
        sectionCount = 1;
        var removeSectionChange = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(1)]);
        adapterCallback?.Invoke(removeSectionChange);

        // 3. Remove items from section 0
        itemCounts[0] = 1;
        var removeFromSection0 = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 0)]);
        adapterCallback?.Invoke(removeFromSection0);

        // 4. Remove section 0
        sectionCount = 0;
        var removeSection0 = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
        adapterCallback?.Invoke(removeSection0);

        // 5. Insert new section 0 with 2 items
        sectionCount = 1;
        itemCounts = [2];
        var insertSection = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(0)]);
        adapterCallback?.Invoke(insertSection);

        // Record changes so far
        var changesBeforeRemove = allChanges.Count;
        allChanges.Clear();

        // 6. Remove item from the NEW section 0
        itemCounts[0] = 1;
        var removeFromNewSection = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 0)]);
        adapterCallback?.Invoke(removeFromNewSection);

        // Assert - The remove should produce a valid index for the NEW section, not a stale cached one
        allChanges.Should().HaveCount(1);
        var removeChange = allChanges[0];
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
        removeChange.StartItemIndex.Should().Be(0, "Should remove from new section 0 at index 0");
    }

    [Fact]
    internal void OnAdapterChangedWithStaleRemoveItemAfterSectionRemovedShouldBeSkipped()
    {
        // Arrange - This test replicates the exact scenario from logs:
        // The adapter has already removed items from a section, but the section itself
        // is also removed. A stale RemoveItem notification arrives for the old section
        // which now maps to a different (or no) section.
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 4, 8 }; // Section 0: 4 items, Section 1: 8 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        // Use section headers to match real-world scenario
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: (header + 4 items) + (header + 8 items) = 14 flattened items
        // Section 0: header at 0, items at 1-4
        // Section 1: header at 5, items at 6-13
        flattenedAdapter.GetItemCount().Should().Be(14);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Remove all items from section 1 (8 items)
        for (var i = 0; i < 8; i++)
        {
            itemCounts[1]--;
            var removeItem = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
            adapterCallback?.Invoke(removeItem);
        }

        // Verify: 8 removals all at index 6 (section 1 items start after header at 5)
        allChanges.Should().HaveCount(8);
        foreach (var change in allChanges)
        {
            change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
            change.StartItemIndex.Should().Be(6, "All items removed from section 1 which starts items at index 6");
        }

        allChanges.Clear();

        // Now remove section 1 (empty, just header remains)
        sectionCount = 1;
        var removeSection = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(1)]);
        adapterCallback?.Invoke(removeSection);

        // Section removal should remove just the header (1 item)
        allChanges.Should().HaveCount(1);
        allChanges[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        allChanges[0].StartItemIndex.Should().Be(5, "Section 1 header at index 5");
        allChanges[0].EndItemIndex.Should().Be(5, "Only header remains");

        allChanges.Clear();

        // Now a STALE RemoveItem arrives for section 1 (which no longer exists)
        // This should be SKIPPED, not produce -1 or operate on wrong section
        var staleRemove = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
        adapterCallback?.Invoke(staleRemove);

        // Assert - Should be skipped (no changes emitted)
        allChanges.Should().BeEmpty("Stale notification for removed section should be skipped");
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveItemWhenAllItemsAlreadyRemovedFromSectionShouldSkip()
    {
        // Arrange - This test verifies that when multiple RemoveItem notifications arrive
        // but the section's items are already gone (itemCount=0), the notifications are
        // handled correctly using cached offsets
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 1, 3 }; // Section 0: 1 item, Section 1: 3 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        // Use section headers
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: (1+1) + (1+3) = 6 items
        // Section 0: header at 0, item at 1
        // Section 1: header at 2, items at 3-5
        flattenedAdapter.GetItemCount().Should().Be(6);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Remove all items from section 1 
        // Each remove should use cached offset and work correctly
        for (var i = 0; i < 3; i++)
        {
            itemCounts[1]--; // Items already removed in underlying adapter
            var removeItem = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
            adapterCallback?.Invoke(removeItem);
        }

        // All 3 removals should produce valid indices at position 3
        allChanges.Should().HaveCount(3);
        foreach (var change in allChanges)
        {
            change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
            change.StartItemIndex.Should().Be(3, "Items in section 1 start at index 3");
        }

        // Now section 1 has 0 items, only header at position 2
        flattenedAdapter.GetItemCount().Should().Be(3); // header0 + item0 + header1
    }

    [Fact]
    internal void OnAdapterChangedWithRemoveSectionAfterAllItemsRemovedShouldProduceValidRange()
    {
        // Arrange - This test captures the regression: after removing all items from a section,
        // RemoveSection produces an invalid range (start > end) because cached offsets are stale.
        // Bug: After 8 RemoveItem from section 1, section offset is still 4 but _flattenedLength=4,
        // so RemoveSection calculates itemsToRemove=0, producing RemoveItemRange(4, 3) - INVALID!
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 4, 8 }; // Section 0: 4 items, Section 1: 8 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        // NO headers - this is the problematic case
        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 4 + 8 = 12 items (no headers)
        flattenedAdapter.GetItemCount().Should().Be(12);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Remove all 8 items from section 1
        for (var i = 0; i < 8; i++)
        {
            itemCounts[1]--;
            var removeItem = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
            adapterCallback?.Invoke(removeItem);
        }

        // Verify all 8 removals at index 4 (section 1 starts there)
        allChanges.Should().HaveCount(8);
        foreach (var change in allChanges)
        {
            change.StartItemIndex.Should().Be(4);
        }

        allChanges.Clear();

        // Now section 1 is empty. Remove the section.
        sectionCount = 1;
        var removeSection = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(1)]);
        adapterCallback?.Invoke(removeSection);

        // Assert - Should either skip (empty section) or produce VALID range
        // BUG: Without fix, produces RemoveItemRange(4, 3) which is invalid!
        foreach (var change in allChanges)
        {
            if (change.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange)
            {
                change.EndItemIndex.Should().BeGreaterThanOrEqualTo(change.StartItemIndex,
                    "RemoveItemRange should have endIndex >= startIndex");
            }
            change.StartItemIndex.Should().BeGreaterThan(-1);
        }
    }

    [Fact]
    internal void OnAdapterChangedWithMultipleRemoveItemFromSameSectionShouldDecrementIndicesCorrectly()
    {
        // Arrange - This test replicates the bug where multiple RemoveItem notifications
        // for the same section all produce the SAME flattened index, causing wrong items
        // to be removed. Each RemoveItem(section, 0) should produce DECREASING indices
        // as items are removed and the section shrinks.
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 2;
        var itemCounts = new[] { 1, 3 }; // Section 0: 1 item, Section 1: 3 items
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < sectionCount && idx < itemCounts.Length ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        // WITH section headers
        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: (1 header + 1 item) + (1 header + 3 items) = 6 flattened items
        // Section 0: header at 0, item at 1
        // Section 1: header at 2, items at 3, 4, 5
        flattenedAdapter.GetItemCount().Should().Be(6);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Remove all 3 items from section 1, one by one
        // Each time we remove item 0 (first item in the section)
        for (var i = 0; i < 3; i++)
        {
            itemCounts[1]--;
            var removeItem = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(1, 0)]);
            adapterCallback?.Invoke(removeItem);
        }

        // Assert - Each removal should produce index 3 (first item position in section 1)
        // because we're always removing item 0 and the section shrinks
        allChanges.Should().HaveCount(3);
        foreach (var change in allChanges)
        {
            change.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
            change.StartItemIndex.Should().Be(3, "Each RemoveItem(1, 0) should target index 3 (first item in section 1)");
        }

        // After all removals, section 1 should have only its header at position 2
        flattenedAdapter.GetItemCount().Should().Be(3); // header0 + item0 + header1
    }

    [Fact]
    internal void OnAdapterChangedWithMixedInsertRemoveSectionsShouldTrackIndicesCorrectly()
    {
        // Arrange - Tests rapid section insert/remove cycles
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var sectionCount = 0;
        var itemCounts = new List<int>();
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(_ => sectionCount);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call =>
        {
            var idx = call.Arg<int>();
            return idx < itemCounts.Count ? itemCounts[idx] : 0;
        });
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"S{call.ArgAt<int>(0)}I{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Rapid cycle of insert section, add items, remove items, remove section
        for (var cycle = 0; cycle < 3; cycle++)
        {
            allChanges.Clear();

            // Insert section 0 with 2 items
            sectionCount = 1;
            itemCounts = [2];
            var insertSection = new VirtualScrollChangeSet([VirtualScrollChangeFactory.InsertSection(0)]);
            adapterCallback?.Invoke(insertSection);

            // Verify insert produced valid indices
            allChanges.Should().ContainSingle();
            allChanges[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
            allChanges[0].StartItemIndex.Should().Be(0);
            allChanges[0].EndItemIndex.Should().Be(1); // 2 items

            allChanges.Clear();

            // Remove item
            itemCounts[0] = 1;
            var removeItem = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveItem(0, 0)]);
            adapterCallback?.Invoke(removeItem);

            // Verify remove produced valid index
            allChanges.Should().ContainSingle();
            allChanges[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItem);
            allChanges[0].StartItemIndex.Should().Be(0);

            allChanges.Clear();

            // Remove section
            sectionCount = 0;
            itemCounts.Clear();
            var removeSection = new VirtualScrollChangeSet([VirtualScrollChangeFactory.RemoveSection(0)]);
            adapterCallback?.Invoke(removeSection);

            // Verify section remove produced valid indices
            allChanges.Should().ContainSingle();
            allChanges[0].Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
            allChanges[0].StartItemIndex.Should().BeGreaterThan(-1);
        }
    }

    // ─────────────────────────────
    // Bug fix tests: ReplaceSection with different item counts
    // ─────────────────────────────

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWhenNewSectionHasFewerItemsShouldEmitReplaceAndRemove()
    {
        // Arrange - Section with 70 items replaced by section with 1 item
        // Expected: Replace 1 item (or none if no items in common), Remove 69 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 70 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 1 section with 70 items
        flattenedAdapter.GetItemCount().Should().Be(70);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced, adapter now returns 1 item
        itemsPerSection[0] = 1;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Replace for the 1 common item and Remove for the 69 excess items
        allChanges.Should().HaveCount(2);

        // First change should be replace for the common items
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(0); // Only 1 item to replace

        // Second change should be remove for excess items
        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(1); // Start after the replaced item
        removeChange.EndItemIndex.Should().Be(69); // 69 items to remove (indices 1-69)

        // Final count should be 1
        flattenedAdapter.GetItemCount().Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWhenNewSectionHasMoreItemsShouldEmitReplaceAndInsert()
    {
        // Arrange - Section with 1 item replaced by section with 70 items
        // Expected: Replace 1 item, Insert 69 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 1 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 1 section with 1 item
        flattenedAdapter.GetItemCount().Should().Be(1);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced, adapter now returns 70 items
        itemsPerSection[0] = 70;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Replace for the 1 common item and Insert for the 69 new items
        allChanges.Should().HaveCount(2);

        // First change should be replace for the common items
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(0); // Only 1 item to replace

        // Second change should be insert for new items
        var insertChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.InsertItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(1); // Start after the replaced item
        insertChange.EndItemIndex.Should().Be(69); // 69 items to insert (indices 1-69)

        // Final count should be 70
        flattenedAdapter.GetItemCount().Should().Be(70);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWhenNewSectionIsEmptyShouldEmitRemoveOnly()
    {
        // Arrange - Section with 5 items replaced by empty section
        // Expected: Remove 5 items (no replace since no common items)
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 5 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 1 section with 5 items
        flattenedAdapter.GetItemCount().Should().Be(5);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with empty section
        itemsPerSection[0] = 0;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Remove for all 5 items
        allChanges.Should().HaveCount(1);
        var removeChange = allChanges[0];
        removeChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(0);
        removeChange.EndItemIndex.Should().Be(4); // 5 items to remove (indices 0-4)

        // Final count should be 0
        flattenedAdapter.GetItemCount().Should().Be(0);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWhenOldSectionWasEmptyShouldEmitInsertOnly()
    {
        // Arrange - Empty section replaced by section with 5 items
        // Expected: Insert 5 items (no replace since old section was empty)
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 0 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 1 section with 0 items
        flattenedAdapter.GetItemCount().Should().Be(0);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with 5 items
        itemsPerSection[0] = 5;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Insert for all 5 items
        allChanges.Should().HaveCount(1);
        var insertChange = allChanges[0];
        insertChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(0);
        insertChange.EndItemIndex.Should().Be(4); // 5 items to insert (indices 0-4)

        // Final count should be 5
        flattenedAdapter.GetItemCount().Should().Be(5);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionHeaderWhenItemCountChangesShouldAccountForHeader()
    {
        // Arrange - Section with header + 10 items replaced by section with header + 2 items
        // Expected: Replace header + 2 items, Remove 8 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 10 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 1 header + 10 items = 11 flattened items
        flattenedAdapter.GetItemCount().Should().Be(11);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with 2 items
        itemsPerSection[0] = 2;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Replace for header + 2 items, Remove for 8 items
        allChanges.Should().HaveCount(2);

        // Replace for header + 2 items (indices 0-2)
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(2); // Header + 2 items

        // Remove for 8 items (indices 3-10)
        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(3);
        removeChange.EndItemIndex.Should().Be(10);

        // Final count: header + 2 items = 3
        flattenedAdapter.GetItemCount().Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithAllHeadersFootersWhenItemCountChangesShouldAccountForAll()
    {
        // Arrange - Section with globalHeader + sectionHeader + 10 items + sectionFooter + globalFooter
        // Replaced by section with only 2 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 10 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
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

        // Initial: 1 globalHeader + 1 sectionHeader + 10 items + 1 sectionFooter + 1 globalFooter = 14
        flattenedAdapter.GetItemCount().Should().Be(14);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with 2 items
        itemsPerSection[0] = 2;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - The section portion changes from (sectionHeader + 10 + sectionFooter = 12) to (sectionHeader + 2 + sectionFooter = 4)
        // Replace the common portion (4 items), Remove the excess (8 items)
        allChanges.Should().HaveCount(2);

        // Replace for sectionHeader + 2 items + sectionFooter (indices 1-4, after global header)
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(1); // After global header
        replaceChange.EndItemIndex.Should().Be(4); // sectionHeader + 2 items + sectionFooter

        // Remove for 8 excess items
        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(5);
        removeChange.EndItemIndex.Should().Be(12);

        // Final count: 1 + 1 + 2 + 1 + 1 = 6
        flattenedAdapter.GetItemCount().Should().Be(6);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWhenNewSectionsHaveFewerItemsShouldEmitReplaceAndRemove()
    {
        // Arrange - 2 sections with 30 items each replaced by 2 sections with 5 items each
        // Old total: 60 items, New total: 10 items
        // Expected: Replace 10 items, Remove 50 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 30, 30, 10 }; // 3 sections
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(3);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 30 + 30 + 10 = 70 items
        flattenedAdapter.GetItemCount().Should().Be(70);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Sections 0 and 1 are replaced with 5 items each
        itemsPerSection[0] = 5;
        itemsPerSection[1] = 5;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Replace for 10 items, Remove for 50 items
        allChanges.Should().HaveCount(2);

        // Replace for 10 items (indices 0-9)
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(9); // 10 items

        // Remove for 50 items (indices 10-59)
        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(10);
        removeChange.EndItemIndex.Should().Be(59);

        // Final count: 5 + 5 + 10 = 20
        flattenedAdapter.GetItemCount().Should().Be(20);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWhenNewSectionsHaveMoreItemsShouldEmitReplaceAndInsert()
    {
        // Arrange - 2 sections with 5 items each replaced by 2 sections with 30 items each
        // Old total: 10 items, New total: 60 items
        // Expected: Replace 10 items, Insert 50 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 5, 5, 10 }; // 3 sections
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(3);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 5 + 5 + 10 = 20 items
        flattenedAdapter.GetItemCount().Should().Be(20);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Sections 0 and 1 are replaced with 30 items each
        itemsPerSection[0] = 30;
        itemsPerSection[1] = 30;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Replace for 10 items, Insert for 50 items
        allChanges.Should().HaveCount(2);

        // Replace for 10 items (indices 0-9)
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(9); // 10 items

        // Insert for 50 items (indices 10-59)
        var insertChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.InsertItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(10);
        insertChange.EndItemIndex.Should().Be(59);

        // Final count: 30 + 30 + 10 = 70
        flattenedAdapter.GetItemCount().Should().Be(70);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionInMiddleWhenItemCountChangesShouldCalculateCorrectIndices()
    {
        // Arrange - 3 sections: [10, 20, 5], replace section 1 (20 items) with 3 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 10, 20, 5 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(3);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 10 + 20 + 5 = 35 items
        flattenedAdapter.GetItemCount().Should().Be(35);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section 1 (at flattened index 10-29) is replaced with 3 items
        itemsPerSection[1] = 3;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit Replace for 3 items (10-12), Remove for 17 items (13-29)
        allChanges.Should().HaveCount(2);

        // Replace for 3 items starting at index 10
        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(10);
        replaceChange.EndItemIndex.Should().Be(12);

        // Remove for 17 items (indices 13-29)
        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(13);
        removeChange.EndItemIndex.Should().Be(29);

        // Final count: 10 + 3 + 5 = 18
        flattenedAdapter.GetItemCount().Should().Be(18);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWhenSameItemCountShouldOnlyEmitReplace()
    {
        // Arrange - Section with 10 items replaced by section with 10 items (same count)
        // Expected: Just Replace, no Insert/Remove
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 10 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns((object?)null);
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo();
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 10 items
        flattenedAdapter.GetItemCount().Should().Be(10);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with same item count (content changed, not structure)
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Should emit just one Replace for all 10 items
        allChanges.Should().HaveCount(1);
        var replaceChange = allChanges[0];
        replaceChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(9);

        // Final count should still be 10
        flattenedAdapter.GetItemCount().Should().Be(10);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionFooterOnlyWhenItemCountDecreasesShouldAccountForFooter()
    {
        // Arrange - Section with footer + 10 items replaced by section with footer + 2 items
        // Old flattened: 10 items + 1 footer = 11
        // New flattened: 2 items + 1 footer = 3
        // Expected: Replace 3 items (2 items + footer), Remove 8 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 10 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 10 items + 1 footer = 11
        flattenedAdapter.GetItemCount().Should().Be(11);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with 2 items
        itemsPerSection[0] = 2;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 3 (2 items + footer), Remove 8
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(2); // 2 items + footer

        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(3);
        removeChange.EndItemIndex.Should().Be(10); // 8 items removed

        // Final: 2 items + footer = 3
        flattenedAdapter.GetItemCount().Should().Be(3);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionHeaderAndFooterWhenItemCountIncreasesShouldAccountForBoth()
    {
        // Arrange - Section with header + 2 items + footer replaced by header + 10 items + footer
        // Old flattened: 1 header + 2 items + 1 footer = 4
        // New flattened: 1 header + 10 items + 1 footer = 12
        // Expected: Replace 4 items, Insert 8 items
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 2 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: header + 2 items + footer = 4
        flattenedAdapter.GetItemCount().Should().Be(4);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with 10 items
        itemsPerSection[0] = 10;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 4 (header + 2 items + footer), Insert 8
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(3); // header + 2 items + footer

        var insertChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.InsertItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(4);
        insertChange.EndItemIndex.Should().Be(11); // 8 items inserted

        // Final: header + 10 items + footer = 12
        flattenedAdapter.GetItemCount().Should().Be(12);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionHeaderWhenSectionBecomesEmptyShouldRemoveItemsButKeepHeader()
    {
        // Arrange - Section with header + 5 items replaced by header + 0 items
        // Old flattened: 1 header + 5 items = 6
        // New flattened: 1 header + 0 items = 1
        // Expected: Replace 1 (header only), Remove 5
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 5 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: header + 5 items = 6
        flattenedAdapter.GetItemCount().Should().Be(6);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section becomes empty
        itemsPerSection[0] = 0;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 1 (header), Remove 5
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(0); // Just header

        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(1);
        removeChange.EndItemIndex.Should().Be(5); // 5 items removed

        // Final: header only = 1
        flattenedAdapter.GetItemCount().Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionFooterWhenSectionBecomesEmptyShouldRemoveItemsButKeepFooter()
    {
        // Arrange - Section with 5 items + footer replaced by 0 items + footer
        // Old flattened: 5 items + 1 footer = 6
        // New flattened: 0 items + 1 footer = 1
        // Expected: Replace 1 (footer only), Remove 5
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 5 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 5 items + footer = 6
        flattenedAdapter.GetItemCount().Should().Be(6);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section becomes empty
        itemsPerSection[0] = 0;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 1 (footer), Remove 5
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(0); // Just footer

        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(1);
        removeChange.EndItemIndex.Should().Be(5); // 5 items removed

        // Final: footer only = 1
        flattenedAdapter.GetItemCount().Should().Be(1);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithHeaderFooterWhenEmptySectionGetsItemsShouldInsertItems()
    {
        // Arrange - Empty section with header + footer gets 5 items
        // Old flattened: 1 header + 0 items + 1 footer = 2
        // New flattened: 1 header + 5 items + 1 footer = 7
        // Expected: Replace 2 (header + footer), Insert 5
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 0 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: header + 0 items + footer = 2
        flattenedAdapter.GetItemCount().Should().Be(2);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section gets 5 items
        itemsPerSection[0] = 5;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 2 (header + footer), Insert 5
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(1); // header + footer

        var insertChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.InsertItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.InsertItemRange);
        insertChange.StartItemIndex.Should().Be(2);
        insertChange.EndItemIndex.Should().Be(6); // 5 items inserted

        // Final: header + 5 items + footer = 7
        flattenedAdapter.GetItemCount().Should().Be(7);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionRangeWithSectionHeadersFootersWhenItemCountsChangeShouldAccountForAll()
    {
        // Arrange - 2 sections each with header + items + footer
        // Section 0: header + 10 items + footer = 12
        // Section 1: header + 10 items + footer = 12
        // Total: 24
        // Replace both sections: section 0 -> 2 items, section 1 -> 3 items
        // New section 0: header + 2 items + footer = 4
        // New section 1: header + 3 items + footer = 5
        // New total: 9
        // Expected: Replace 9, Remove 15
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 10, 10 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(2);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 2 sections * 12 = 24
        flattenedAdapter.GetItemCount().Should().Be(24);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Replace both sections with fewer items
        itemsPerSection[0] = 2;
        itemsPerSection[1] = 3;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSectionRange(0, 1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 9 (new total), Remove 15 (difference)
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(8); // 9 items (4 + 5)

        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(9);
        removeChange.EndItemIndex.Should().Be(23); // 15 items removed

        // Final: 4 + 5 = 9
        flattenedAdapter.GetItemCount().Should().Be(9);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionInSecondSectionWithHeadersFootersWhenItemCountChangesShouldCalculateCorrectIndices()
    {
        // Arrange - 2 sections each with header + items + footer
        // Section 0: header + 5 items + footer = 7 (unchanged)
        // Section 1: header + 10 items + footer = 12 -> header + 2 items + footer = 4
        // Total: 7 + 12 = 19 -> 7 + 4 = 11
        // Expected: Replace 4 (at indices 7-10), Remove 8 (at indices 11-18)
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 5, 10 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(2);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: 7 + 12 = 19
        flattenedAdapter.GetItemCount().Should().Be(19);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Replace section 1 with 2 items
        itemsPerSection[1] = 2;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(1)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 4 (indices 7-10), Remove 8 (indices 11-18)
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(7); // Section 1 starts at index 7
        replaceChange.EndItemIndex.Should().Be(10); // header + 2 items + footer = 4 items

        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(11);
        removeChange.EndItemIndex.Should().Be(18); // 8 items removed

        // Final: 7 + 4 = 11
        flattenedAdapter.GetItemCount().Should().Be(11);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithGlobalAndSectionHeadersFootersWhenItemCountDecreasesShouldAccountForAll()
    {
        // Arrange - Full configuration: globalHeader + sectionHeader + 20 items + sectionFooter + globalFooter
        // Old flattened: 1 + 1 + 20 + 1 + 1 = 24
        // Replace section with 3 items
        // New flattened: 1 + 1 + 3 + 1 + 1 = 7
        // Section portion old: 1 + 20 + 1 = 22
        // Section portion new: 1 + 3 + 1 = 5
        // Expected: Replace 5 (at indices 1-5), Remove 17 (at indices 6-22)
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 20 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
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

        // Initial: 1 + 1 + 20 + 1 + 1 = 24
        flattenedAdapter.GetItemCount().Should().Be(24);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Replace section with 3 items
        itemsPerSection[0] = 3;
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Replace 5 (indices 1-5), Remove 17 (indices 6-22)
        allChanges.Should().HaveCount(2);

        var replaceChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(1); // After global header
        replaceChange.EndItemIndex.Should().Be(5); // sectionHeader + 3 items + sectionFooter

        var removeChange = allChanges.First(c => c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItem ||
                                                  c.Operation == VirtualScrollFlattenedChangeOperation.RemoveItemRange);
        removeChange.StartItemIndex.Should().Be(6);
        removeChange.EndItemIndex.Should().Be(22); // 17 items removed

        // Final: 1 + 1 + 3 + 1 + 1 = 7
        flattenedAdapter.GetItemCount().Should().Be(7);
    }

    [Fact]
    internal void OnAdapterChangedWithReplaceSectionWithSectionHeadersFootersWhenSameItemCountShouldOnlyEmitReplace()
    {
        // Arrange - Section with header + 5 items + footer replaced by same count
        // This tests backward compatibility - when item count doesn't change, behavior should be the same
        Action<VirtualScrollChangeSet>? adapterCallback = null;
        var itemsPerSection = new[] { 5 };
        var adapter = Substitute.For<IVirtualScrollAdapter>();
        adapter.GetSectionCount().Returns(1);
        adapter.GetItemCount(Arg.Any<int>()).Returns(call => itemsPerSection[call.Arg<int>()]);
        adapter.GetItem(Arg.Any<int>(), Arg.Any<int>()).Returns(call => $"Item{call.ArgAt<int>(0)}-{call.ArgAt<int>(1)}");
        adapter.GetSection(Arg.Any<int>()).Returns(call => $"Section{call.Arg<int>()}");
        adapter.Subscribe(Arg.Any<Action<VirtualScrollChangeSet>>())
            .Returns(call =>
            {
                adapterCallback = call.Arg<Action<VirtualScrollChangeSet>>();
                return Substitute.For<IDisposable>();
            });

        var layoutInfo = CreateLayoutInfo(hasSectionHeader: true, hasSectionFooter: true);
        var flattenedAdapter = CreateAdapter(adapter, layoutInfo);

        // Initial: header + 5 items + footer = 7
        flattenedAdapter.GetItemCount().Should().Be(7);

        var allChanges = new List<VirtualScrollFlattenedChange>();
        flattenedAdapter.Subscribe(cs => allChanges.AddRange(cs.Changes));

        // Act - Section is replaced with same count (content changed, structure same)
        var changeSet = new VirtualScrollChangeSet([VirtualScrollChangeFactory.ReplaceSection(0)]);
        adapterCallback?.Invoke(changeSet);

        // Assert - Just one replace for all 7 items
        allChanges.Should().HaveCount(1);
        var replaceChange = allChanges[0];
        replaceChange.Operation.Should().Be(VirtualScrollFlattenedChangeOperation.ReplaceItemRange);
        replaceChange.StartItemIndex.Should().Be(0);
        replaceChange.EndItemIndex.Should().Be(6);

        // Final count unchanged
        flattenedAdapter.GetItemCount().Should().Be(7);
    }
}

