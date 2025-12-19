using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace Nalu.Maui.Sample.PageModels;

public partial class TwelvePageModel : ObservableObject
{
    private static int _instanceCount;
    private static readonly string[] _ownerNames =
    [
        "John Smith", "Emily Johnson", "Michael Brown", "Sarah Davis", "David Wilson",
        "Jessica Martinez", "Christopher Anderson", "Amanda Taylor", "Matthew Thomas", "Ashley Jackson",
        "Daniel White", "Michelle Harris", "Andrew Martin", "Stephanie Thompson", "Joshua Garcia",
        "Nicole Martinez", "Ryan Rodriguez", "Lauren Lewis", "Kevin Lee", "Rachel Walker",
        "Brandon Hall", "Megan Allen", "Justin Young", "Samantha King", "Tyler Wright",
        "Brittany Lopez", "Jordan Hill", "Kayla Scott", "Austin Green", "Taylor Adams",
        "Cameron Baker", "Morgan Nelson", "Dylan Carter", "Alexis Mitchell", "Logan Perez",
        "Jordan Roberts", "Casey Turner", "Riley Phillips", "Quinn Campbell", "Avery Parker",
        "Blake Evans", "Skylar Edwards", "Reese Collins", "Sage Stewart", "River Sanchez",
        "Phoenix Morris", "Rowan Rogers", "Sage Reed", "River Cook", "Phoenix Morgan",
        "Rowan Bell", "Sage Murphy", "River Bailey", "Phoenix Rivera", "Rowan Cooper",
        "Sage Richardson", "River Cox", "Phoenix Howard", "Rowan Ward", "Sage Torres",
        "River Peterson", "Phoenix Gray", "Rowan Ramirez", "Sage James", "River Watson",
        "Phoenix Brooks", "Rowan Kelly", "Sage Sanders", "River Price", "Phoenix Bennett",
        "Rowan Wood", "Sage Barnes", "River Ross", "Phoenix Henderson", "Rowan Coleman",
        "Sage Jenkins", "River Perry", "Phoenix Powell", "Rowan Long", "Sage Patterson",
        "River Hughes", "Phoenix Flores", "Rowan Washington", "Sage Butler", "River Simmons",
        "Phoenix Foster", "Rowan Gonzales", "Sage Bryant", "River Alexander", "Phoenix Russell",
        "Rowan Griffin", "Sage Diaz", "River Hayes", "Phoenix Myers", "Rowan Ford",
        "Sage Hamilton", "River Graham", "Phoenix Sullivan", "Rowan Wallace", "Sage Woods"
    ];

    private static readonly ImageSource[] _cardImageSources = 
    [
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1518791841217-8f162f1e1131?ixlib=rb-4.1.0&q=85&fm=jpg&crop=entropy&cs=srgb&dl=erik-jan-leusink-IbPxGLgJiMI-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1517331156700-3c241d2b4d83?ixlib=rb-4.1.0&q=85&fm=jpg&crop=entropy&cs=srgb&dl=jari-hytonen-YCPkW_r_6uA-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1516280030429-27679b3dc9cf?ixlib=rb-4.1.0&q=85&fm=jpg&crop=entropy&cs=srgb&dl=raquel-pedrotti-AHgpNYkX9dc-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1583795128727-6ec3642408f8?ixlib=rb-4.1.0&q=85&fm=jpg&crop=entropy&cs=srgb&dl=lloyd-henneman-mBRfYA0dYYE-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1532386236358-a33d8a9434e3?ixlib=rb-4.1.0&q=85&fm=jpg&crop=entropy&cs=srgb&dl=raul-varzar-1l2waV8glIQ-unsplash.jpg"))
    ];

    public string Message { get; } = "Credit Cards VirtualScroll Demo - 100 Sections, 30 Items Each";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    public ObservableCollection<TwelveGroup> Groups { get; }

    public TwelveGroupedAdapter Adapter { get; }

    public TwelvePageModel()
    {
        Groups = new ObservableCollection<TwelveGroup>(
            Enumerable.Range(0, 100).Select(i => CreateGroup(i))
        );
        Adapter = new TwelveGroupedAdapter(Groups);
    }

    private TwelveGroup CreateGroup(int index)
    {
        var ownerName = _ownerNames[index % _ownerNames.Length];
        var items = Enumerable.Range(1, 30)
            .Select(i => CreateCreditCard(i))
            .ToList();
        return new TwelveGroup(ownerName, items);
    }

    private TwelveCreditCard CreateCreditCard(int cardNumber)
    {
        var rand = Random.Shared;
        return new TwelveCreditCard
        {
            Name = rand.Next(0, 2) == 1 ? $"Regular card {cardNumber}" : $"Premium card {cardNumber}",
            Type = cardNumber % 2 == 0 ? "Visa" : "MasterCard",
            Exp = $"{rand.Next(1, 13):D2}/{rand.Next(24, 30)}",
            Credit = rand.Next(100, 5000),
            Starred = rand.Next(0, 2) == 1,
            ImageSource = _cardImageSources[rand.Next(_cardImageSources.Length)]
        };
    }
}

/// <summary>
/// Represents a group/section in the VirtualScroll (owner's credit cards).
/// </summary>
public partial class TwelveGroup : ObservableObject, IEnumerable<TwelveCreditCard>
{
    public string OwnerName { get; }
    
    public ObservableCollection<TwelveCreditCard> Items { get; }

    public TwelveGroup(string ownerName, IEnumerable<TwelveCreditCard> items)
    {
        OwnerName = ownerName;
        Items = new ObservableCollection<TwelveCreditCard>(items);
    }

    public IEnumerator<TwelveCreditCard> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Items.GetEnumerator();
}

/// <summary>
/// Represents a credit card item within a group.
/// </summary>
public partial class TwelveCreditCard : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }
    
    [ObservableProperty]
    public partial string Type { get; set; }
    
    [ObservableProperty]
    public partial string Exp { get; set; }

    [ObservableProperty]
    public partial bool Starred { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreditDollar))]
    public partial double Credit { get; set; }

    [ObservableProperty]
    public partial ImageSource? ImageSource { get; set; }
    
    public string CreditDollar => Credit.ToString("C2");

    [RelayCommand]
    public void StarUnstar() => Starred = !Starred;
}

/// <summary>
/// Custom adapter for grouped/sectioned credit card data in VirtualScroll.
/// </summary>
public sealed class TwelveGroupedAdapter : IVirtualScrollAdapter, IDisposable
{
    private readonly ObservableCollection<TwelveGroup> _groups;
    private readonly List<Subscription> _subscriptions = [];

    public TwelveGroupedAdapter(ObservableCollection<TwelveGroup> groups)
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
        private readonly TwelveGroupedAdapter _adapter;
        private readonly ObservableCollection<TwelveGroup> _groups;
        private readonly Action<VirtualScrollChangeSet> _changeCallback;
        private readonly Dictionary<TwelveGroup, NotifyCollectionChangedEventHandler> _itemHandlers = new();
        private bool _disposed;

        public Subscription(
            TwelveGroupedAdapter adapter,
            ObservableCollection<TwelveGroup> groups,
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

        private void SubscribeToGroupItems(TwelveGroup group)
        {
            void Handler(object? sender, NotifyCollectionChangedEventArgs e) => OnItemsChanged(group, e);
            _itemHandlers[group] = Handler;
            group.Items.CollectionChanged += Handler;
        }

        private void UnsubscribeFromGroupItems(TwelveGroup group)
        {
            if (_itemHandlers.TryGetValue(group, out var handler))
            {
                group.Items.CollectionChanged -= handler;
                _itemHandlers.Remove(group);
            }
        }

        private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var changes = new List<VirtualScrollChange>();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is { Count: > 0 })
                    {
                        foreach (TwelveGroup group in e.NewItems)
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
                        foreach (TwelveGroup group in e.OldItems)
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
                        foreach (TwelveGroup group in e.OldItems)
                        {
                            UnsubscribeFromGroupItems(group);
                        }
                    }
                    if (e.NewItems is { Count: > 0 })
                    {
                        foreach (TwelveGroup group in e.NewItems)
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

        private void OnItemsChanged(TwelveGroup group, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var sectionIndex = _groups.IndexOf(group);
            if (sectionIndex < 0)
            {
                return;
            }

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
            if (_disposed)
            {
                return;
            }

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

