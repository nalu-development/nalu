using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class ElevenPageModel : ObservableObject
{
    private static int _instanceCount;
    private int _groupIdCounter;
    private int _itemIdCounter;

    public string Message { get; } = "Grouped VirtualScroll Demo";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    public ObservableCollection<ElevenGroup> Groups { get; }

    public ElevenGroupedAdapter Adapter { get; }

    public ElevenPageModel()
    {
        Groups = new ObservableCollection<ElevenGroup>(
            Enumerable.Range(1, 5).Select(i => CreateGroup())
        );
        Adapter = new ElevenGroupedAdapter(Groups);
    }

    private ElevenGroup CreateGroup()
    {
        var groupId = ++_groupIdCounter;
        var items = Enumerable.Range(1, Random.Shared.Next(3, 8))
            .Select(_ => new ElevenItem($"Item {++_itemIdCounter}"))
            .ToList();
        return new ElevenGroup($"Group {groupId}", items);
    }

    [RelayCommand]
    private void AddGroup()
    {
        var randomIndex = Groups.Count > 0 ? Random.Shared.Next(Groups.Count + 1) : 0;
        Groups.Insert(randomIndex, CreateGroup());
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (Groups.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Groups.Count);
            Groups.RemoveAt(randomIndex);
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        if (Groups.Count > 0)
        {
            var groupIndex = Random.Shared.Next(Groups.Count);
            var group = Groups[groupIndex];
            var itemIndex = group.Items.Count > 0 ? Random.Shared.Next(group.Items.Count + 1) : 0;
            group.Items.Insert(itemIndex, new ElevenItem($"Item {++_itemIdCounter}"));
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        var nonEmptyGroups = Groups.Where(g => g.Items.Count > 0).ToList();
        if (nonEmptyGroups.Count > 0)
        {
            var group = nonEmptyGroups[Random.Shared.Next(nonEmptyGroups.Count)];
            var itemIndex = Random.Shared.Next(group.Items.Count);
            group.Items.RemoveAt(itemIndex);
        }
    }

    [RelayCommand]
    private void MoveItem()
    {
        var nonEmptyGroups = Groups.Where(g => g.Items.Count > 1).ToList();
        if (nonEmptyGroups.Count > 0)
        {
            var group = nonEmptyGroups[Random.Shared.Next(nonEmptyGroups.Count)];
            var fromIndex = Random.Shared.Next(group.Items.Count);
            int toIndex;
            do
            {
                toIndex = Random.Shared.Next(group.Items.Count);
            } while (toIndex == fromIndex);
            
            group.Items.Move(fromIndex, toIndex);
        }
    }
}

/// <summary>
/// Represents a group/section in the VirtualScroll.
/// </summary>
public partial class ElevenGroup : ObservableObject
{
    public string Title { get; }
    
    public ObservableCollection<ElevenItem> Items { get; }

    public ElevenGroup(string title, IEnumerable<ElevenItem> items)
    {
        Title = title;
        Items = new ObservableCollection<ElevenItem>(items);
    }
}

/// <summary>
/// Represents an item within a group.
/// </summary>
public partial class ElevenItem : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [RelayCommand]
    private void Tap()
    {
        Name += " (tapped)";
    }

    public ElevenItem(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Custom adapter for grouped/sectioned data in VirtualScroll.
/// </summary>
public sealed class ElevenGroupedAdapter : IVirtualScrollAdapter, IDisposable
{
    private readonly ObservableCollection<ElevenGroup> _groups;
    private readonly List<Subscription> _subscriptions = [];

    public ElevenGroupedAdapter(ObservableCollection<ElevenGroup> groups)
    {
        _groups = groups;
    }

    public int GetSectionCount() => _groups.Count;

    public int GetItemCount(int sectionIndex) => _groups[sectionIndex].Items.Count;

    public object? GetSection(int sectionIndex) => _groups[sectionIndex];

    public object? GetItem(int sectionIndex, int itemIndex) => _groups[sectionIndex].Items[itemIndex];

    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback)
    {
        var subscription = new Subscription(this, _groups, changeCallback);
        _subscriptions.Add(subscription);
        return subscription;
    }

    private void RemoveSubscription(Subscription subscription)
    {
        _subscriptions.Remove(subscription);
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions.ToList())
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ElevenGroupedAdapter _adapter;
        private readonly ObservableCollection<ElevenGroup> _groups;
        private readonly Action<VirtualScrollChangeSet> _changeCallback;
        private readonly Dictionary<ElevenGroup, NotifyCollectionChangedEventHandler> _itemHandlers = new();
        private bool _disposed;

        public Subscription(
            ElevenGroupedAdapter adapter,
            ObservableCollection<ElevenGroup> groups,
            Action<VirtualScrollChangeSet> changeCallback)
        {
            _adapter = adapter;
            _groups = groups;
            _changeCallback = changeCallback;

            // Subscribe to group collection changes
            _groups.CollectionChanged += OnGroupsChanged;

            // Subscribe to each group's items collection
            foreach (var group in _groups)
            {
                SubscribeToGroupItems(group);
            }
        }

        private void SubscribeToGroupItems(ElevenGroup group)
        {
            void Handler(object? sender, NotifyCollectionChangedEventArgs e) => OnItemsChanged(group, e);
            _itemHandlers[group] = Handler;
            group.Items.CollectionChanged += Handler;
        }

        private void UnsubscribeFromGroupItems(ElevenGroup group)
        {
            if (_itemHandlers.TryGetValue(group, out var handler))
            {
                group.Items.CollectionChanged -= handler;
                _itemHandlers.Remove(group);
            }
        }

        private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed) return;

            var changes = new List<VirtualScrollChange>();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is { Count: > 0 })
                    {
                        foreach (ElevenGroup group in e.NewItems)
                        {
                            SubscribeToGroupItems(group);
                        }

                        if (e.NewItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.InsertSection(e.NewStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.InsertSectionRange(e.NewStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is { Count: > 0 })
                    {
                        foreach (ElevenGroup group in e.OldItems)
                        {
                            UnsubscribeFromGroupItems(group);
                        }

                        if (e.OldItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.RemoveSection(e.OldStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.OldStartingIndex + e.OldItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.RemoveSectionRange(e.OldStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is { Count: 1 })
                    {
                        changes.Add(VirtualScrollChangeFactory.MoveSection(e.OldStartingIndex, e.NewStartingIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems is { Count: > 0 })
                    {
                        foreach (ElevenGroup group in e.OldItems)
                        {
                            UnsubscribeFromGroupItems(group);
                        }
                    }
                    if (e.NewItems is { Count: > 0 })
                    {
                        foreach (ElevenGroup group in e.NewItems)
                        {
                            SubscribeToGroupItems(group);
                        }

                        if (e.NewItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.ReplaceSection(e.NewStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.ReplaceSectionRange(e.NewStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Unsubscribe from all item handlers
                    foreach (var group in _itemHandlers.Keys.ToList())
                    {
                        UnsubscribeFromGroupItems(group);
                    }
                    // Re-subscribe to current groups
                    foreach (var group in _groups)
                    {
                        SubscribeToGroupItems(group);
                    }
                    changes.Add(VirtualScrollChangeFactory.Reset());
                    break;
            }

            if (changes.Count > 0)
            {
                _changeCallback(new VirtualScrollChangeSet(changes));
            }
        }

        private void OnItemsChanged(ElevenGroup group, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed) return;

            var sectionIndex = _groups.IndexOf(group);
            if (sectionIndex < 0) return;

            var changes = new List<VirtualScrollChange>();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is { Count: > 0 })
                    {
                        if (e.NewItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.InsertItem(sectionIndex, e.NewStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.InsertItemRange(sectionIndex, e.NewStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is { Count: > 0 })
                    {
                        if (e.OldItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.RemoveItem(sectionIndex, e.OldStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.OldStartingIndex + e.OldItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.RemoveItemRange(sectionIndex, e.OldStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is { Count: 1 })
                    {
                        changes.Add(VirtualScrollChangeFactory.MoveItem(sectionIndex, e.OldStartingIndex, e.NewStartingIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems is { Count: > 0 })
                    {
                        if (e.NewItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.ReplaceItem(sectionIndex, e.NewStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.ReplaceItemRange(sectionIndex, e.NewStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    changes.Add(VirtualScrollChangeFactory.ReplaceSection(sectionIndex));
                    break;
            }

            if (changes.Count > 0)
            {
                _changeCallback(new VirtualScrollChangeSet(changes));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _groups.CollectionChanged -= OnGroupsChanged;

            foreach (var group in _itemHandlers.Keys.ToList())
            {
                UnsubscribeFromGroupItems(group);
            }

            _adapter.RemoveSubscription(this);
        }
    }
}
