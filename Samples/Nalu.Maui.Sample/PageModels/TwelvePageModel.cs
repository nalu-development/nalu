using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class TwelvePageStaticModel : ObservableObject
{
    public static TwelvePageStaticModel Instance { get; } = new();
    
    [ObservableProperty]
    public partial string Message { get; set; } = "Credit Cards Demo";
}

public partial class TwelvePageModel : ObservableObject, IDisposable
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
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1518791841217-8f162f1e1131?ixlib=rb-4.1.0&q=85&fm=jpg&w=480&crop=entropy&cs=srgb&dl=erik-jan-leusink-IbPxGLgJiMI-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1517331156700-3c241d2b4d83?ixlib=rb-4.1.0&q=85&fm=jpg&w=480&crop=entropy&cs=srgb&dl=jari-hytonen-YCPkW_r_6uA-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1516280030429-27679b3dc9cf?ixlib=rb-4.1.0&q=85&fm=jpg&w=480&crop=entropy&cs=srgb&dl=raquel-pedrotti-AHgpNYkX9dc-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1583795128727-6ec3642408f8?ixlib=rb-4.1.0&q=85&fm=jpg&w=480&crop=entropy&cs=srgb&dl=lloyd-henneman-mBRfYA0dYYE-unsplash.jpg")),
        ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1532386236358-a33d8a9434e3?ixlib=rb-4.1.0&q=85&fm=jpg&w=480&crop=entropy&cs=srgb&dl=raul-varzar-1l2waV8glIQ-unsplash.jpg"))
    ];

    public string Message { get; } = "Credit Cards VirtualScroll Demo - 100 Sections, 30 Items Each";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    public ObservableCollection<TwelveGroup> Groups { get; }

    public IVirtualScrollAdapter Adapter { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoneSelected), nameof(IsCollectionViewSelected), nameof(IsVirtualScrollSelected))]
    private bool? _viewMode = null; // null = none, true = CollectionView, false = VirtualScroll

    public bool IsNoneSelected => ViewMode == null;
    public bool IsCollectionViewSelected => ViewMode == true;
    public bool IsVirtualScrollSelected => ViewMode == false;

    [ObservableProperty]
    private int _currentSectionIndex = 0;

    [ObservableProperty]
    private int _currentItemIndex = 0;

    [ObservableProperty]
    private int _scrollTrigger = 0; // Incremented to trigger scroll

    [RelayCommand]
    private void ShowNone() => ViewMode = null;

    [RelayCommand]
    private void ShowCollectionView() => ViewMode = true;

    [RelayCommand]
    private void ShowVirtualScroll() => ViewMode = false;

    [RelayCommand]
    private async Task StartAutoScrollAsync()
    {
        if (ViewMode == null)
        {
            return;
        }

        // Reset to start
        CurrentSectionIndex = 0;
        CurrentItemIndex = 0;

        for (var i = 0; i < 10; i++)
        {
            // Calculate next position (current item + 50)
            var targetItemIndex = CurrentItemIndex + 50;
            var targetSectionIndex = CurrentSectionIndex;

            // Handle wrapping across sections
            while (targetSectionIndex < Groups.Count && targetItemIndex >= Groups[targetSectionIndex].Items.Count)
            {
                targetItemIndex -= Groups[targetSectionIndex].Items.Count;
                targetSectionIndex++;
            }

            // Clamp to bounds
            if (targetSectionIndex >= Groups.Count)
            {
                targetSectionIndex = Groups.Count - 1;
                targetItemIndex = Groups[targetSectionIndex].Items.Count - 1;
            }
            else if (targetItemIndex >= Groups[targetSectionIndex].Items.Count)
            {
                targetItemIndex = Groups[targetSectionIndex].Items.Count - 1;
            }

            CurrentSectionIndex = targetSectionIndex;
            CurrentItemIndex = targetItemIndex;

            // Trigger scroll by incrementing the trigger
            ScrollTrigger++;

            await Task.Delay(1000); // Small delay between scrolls
        }
    }

    public TwelvePageModel()
    {
        Groups = new ObservableCollection<TwelveGroup>(
            Enumerable.Range(0, 100).Select(i => CreateGroup(i))
        );
        Adapter = VirtualScroll.CreateObservableCollectionAdapter(Groups, g => g.Items);
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

    private static readonly LeakDetector _leakDetector = new();

    public void Dispose()
    {
        var sections = Adapter.GetSectionCount();

        for (var s = 0; s < sections; s++)
        {
            var section = Adapter.GetSection(s);
            _leakDetector.Track(section);
            
            var items = Adapter.GetItemCount(s);
            for (var i = 0; i < items; i++)
            {
                var item = Adapter.GetItem(s, i);
                _leakDetector.Track(item);
            }
        }
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
