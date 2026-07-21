using System.Collections.ObjectModel;
using DynamicData;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Deterministic pseudo-random source (LCG) so stress storms are perfectly reproducible:
/// the final section/item counts asserted by the UI tests never change between runs.
/// </summary>
internal sealed class DeterministicRandom(uint seed)
{
    private uint _state = seed;

    public int Next(int maxExclusive)
    {
        _state = (_state * 1664525u) + 1013904223u;

        return (int) ((_state >> 16) % (uint) maxExclusive);
    }
}

/// <summary>
/// Battle-test harness: fires a storm of grouped mutations (1-3 items or 1-2 whole sections
/// added/removed/moved/replaced per 24ms tick) optionally while the list keeps scrolling
/// end-to-start-to-end. UI tests additionally swipe while the storm runs.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll Stress Tests")]
public class VirtualScrollStressTests : ContentPage
{
    private const int TickCount = 120;
    private static readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(24);
    private static readonly TimeSpan _scrollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ObservableCollection<VirtualScrollSection> _sections;
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _statusLabel;
    private readonly Label _lastSectionLabel;
    private DeterministicRandom _rng = new(12345);
    private int _nextItem;
    private int _nextSection = 6;

    public VirtualScrollStressTests()
    {
        _sections = new ObservableCollection<VirtualScrollSection>(
            Enumerable.Range(1, 6).Select(i => new VirtualScrollSection($"S{i}", Enumerable.Range(1, 5).Select(j => new VirtualScrollListItem($"S{i}i{j}"))))
        );

        var adapter = VirtualScroll.CreateObservableCollectionAdapter(_sections, s => s.Items);

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "StressScroll",
                             ItemsSource = adapter,

                             HeaderTemplate = new DataTemplate(() => new Label
                                 {
                                     AutomationId = "StressHeader",
                                     Text = "Stress header",
                                     FontAttributes = FontAttributes.Bold,
                                     HorizontalOptions = LayoutOptions.Center
                                 }
                             ),

                             SectionHeaderTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     FontSize = 15,
                                                     FontAttributes = FontAttributes.Bold,
                                                     BackgroundColor = Colors.LightGray,
                                                     Padding = new Thickness(10, 4)
                                                 };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollSection.Name));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "SH {0}"));

                                     return label;
                                 }
                             ),

                             SectionFooterTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 11, FontAttributes = FontAttributes.Italic, Padding = new Thickness(10, 2) };
                                     label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "end {0}"));

                                     return label;
                                 }
                             ),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 13, Margin = new Thickness(16, 5) };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        _statusLabel = new Label { AutomationId = "StressStatusLabel", FontSize = 13, Text = "Idle" };
        _lastSectionLabel = new Label { AutomationId = "LastSectionLabel", FontSize = 13, Text = "-" };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Storm", "StormButton", () => StartStorm(withScroll: false)),
                                 MakeButton("Storm+scroll", "StormScrollButton", () => StartStorm(withScroll: true)),
                                 MakeButton("First", "ScrollToFirstButton", () => ScrollToSection(0)),
                                 MakeButton("Last", "ScrollToLastButton", () => ScrollToSection(_sections.Count - 1)),
                                 _statusLabel,
                                 _lastSectionLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll, 0, 1);

        Content = grid;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void ScrollToSection(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        _virtualScroll.ScrollTo(index, -1, ScrollToPosition.Start, animated: false);
        _lastSectionLabel.Text = $"SH {_sections[index].Name}";
    }

    private void StartStorm(bool withScroll)
    {
        _statusLabel.Text = "Running";
        _rng = new DeterministicRandom(12345);

        var ticks = 0;
        var mutationTimer = Dispatcher.CreateTimer();
        var scrollTimer = Dispatcher.CreateTimer();

        mutationTimer.Interval = _tickInterval;
        mutationTimer.Tick += (_, _) =>
        {
            Mutate();

            if (++ticks >= TickCount)
            {
                mutationTimer.Stop();
                scrollTimer.Stop();
                _statusLabel.Text = $"Done S:{_sections.Count} I:{_sections.Sum(s => s.Items.Count)}";
            }
        };

        if (withScroll)
        {
            // Programmatic end/start oscillation with animations, concurrent with the storm.
            var toEnd = true;
            scrollTimer.Interval = _scrollInterval;
            scrollTimer.Tick += (_, _) =>
            {
                var lastSection = _sections.Count - 1;

                if (lastSection < 0)
                {
                    return;
                }

                if (toEnd)
                {
                    var lastItem = Math.Max(_sections[lastSection].Items.Count - 1, 0);
                    _virtualScroll.ScrollTo(lastSection, lastItem, ScrollToPosition.End, animated: true);
                }
                else
                {
                    _virtualScroll.ScrollTo(0, 0, ScrollToPosition.Start, animated: true);
                }

                toEnd = !toEnd;
            };
            scrollTimer.Start();
        }

        mutationTimer.Start();
    }

    private VirtualScrollListItem NewItem() => new($"Q{++_nextItem}");

    private VirtualScrollSection NewSection()
    {
        var name = $"T{++_nextSection}";

        return new VirtualScrollSection(name, Enumerable.Range(1, 1 + _rng.Next(3)).Select(_ => NewItem()));
    }

    private void Mutate()
    {
        switch (_rng.Next(10))
        {
            case 0 or 1 or 2:
            {
                // Insert 1-3 items at a random position of a random section.
                var section = _sections[_rng.Next(_sections.Count)];
                var count = 1 + _rng.Next(3);

                for (var i = 0; i < count; i++)
                {
                    section.Items.Insert(_rng.Next(section.Items.Count + 1), NewItem());
                }

                break;
            }

            case 3 or 4:
            {
                // Remove 1-2 items from a random section (empty sections are allowed and stay).
                var section = _sections[_rng.Next(_sections.Count)];
                var count = 1 + _rng.Next(2);

                for (var i = 0; i < count && section.Items.Count > 0; i++)
                {
                    section.Items.RemoveAt(_rng.Next(section.Items.Count));
                }

                break;
            }

            case 5:
                // Insert a new section at a random position.
                _sections.Insert(_rng.Next(_sections.Count + 1), NewSection());

                break;

            case 6:
                // Burst: two sections added in the same pass.
                _sections.Insert(_rng.Next(_sections.Count + 1), NewSection());
                _sections.Insert(_rng.Next(_sections.Count + 1), NewSection());

                break;

            case 7:
                if (_sections.Count > 1)
                {
                    _sections.RemoveAt(_rng.Next(_sections.Count));
                }

                break;

            case 8:
            {
                // Move an item within a random section.
                var section = _sections[_rng.Next(_sections.Count)];

                if (section.Items.Count >= 2)
                {
                    section.Items.Move(_rng.Next(section.Items.Count), _rng.Next(section.Items.Count));
                }

                break;
            }

            case 9:
            {
                // Replace a random item.
                var section = _sections[_rng.Next(_sections.Count)];

                if (section.Items.Count > 0)
                {
                    section.Items[_rng.Next(section.Items.Count)] = NewItem();
                }

                break;
            }
        }
    }
}

/// <summary>
/// Same storm battle-test through the real-world DynamicData pipeline:
/// SourceCache → Group → per-group inner Bind → outer Bind(out ReadOnlyObservableCollection),
/// including 30-record changesets that escalate the Binds to Reset notifications.
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll DynamicData Stress Tests")]
public class VirtualScrollDynamicDataStressTests : ContentPage
{
    private const int TickCount = 120;
    private static readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(24);
    private static readonly TimeSpan _scrollInterval = TimeSpan.FromMilliseconds(250);

    private readonly SourceCache<PushDataRecord, string> _cache = new(r => r.Id);
    private readonly List<PushDataRecord> _records = [];
    private readonly ReadOnlyObservableCollection<DynamicDataSection> _sections;
#pragma warning disable IDE0052
    private readonly IDisposable _subscription;
#pragma warning restore IDE0052
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _statusLabel;
    private readonly Label _lastSectionLabel;
    private DeterministicRandom _rng = new(54321);
    private int _nextId;
    private int _nextGroup;

    public VirtualScrollDynamicDataStressTests()
    {
        // Seed: K1..K5 × 4 items.
        for (var g = 1; g <= 5; g++)
        {
            for (var i = 0; i < 4; i++)
            {
                AddRecord($"K{g}");
            }
        }

        _subscription = _cache.Connect()
                              .Group(r => r.GroupName)
                              .Transform(g => new DynamicDataSection(g))
                              .DisposeMany()
                              .Bind(out _sections)
                              .Subscribe();

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "StressScroll",
                             ItemsSource = VirtualScroll.CreateObservableCollectionAdapter(_sections, s => s.Items),

                             HeaderTemplate = new DataTemplate(() => new Label
                                 {
                                     AutomationId = "StressHeader",
                                     Text = "DD stress header",
                                     FontAttributes = FontAttributes.Bold,
                                     HorizontalOptions = LayoutOptions.Center
                                 }
                             ),

                             SectionHeaderTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     FontSize = 15,
                                                     FontAttributes = FontAttributes.Bold,
                                                     BackgroundColor = Colors.LightGray,
                                                     Padding = new Thickness(10, 4)
                                                 };
                                     label.SetBinding(Label.TextProperty, nameof(DynamicDataSection.Name));
                                     label.SetBinding(AutomationIdProperty, new Binding(nameof(DynamicDataSection.Name), stringFormat: "SH {0}"));

                                     return label;
                                 }
                             ),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 13, Margin = new Thickness(16, 5) };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        _statusLabel = new Label { AutomationId = "StressStatusLabel", FontSize = 13, Text = "Idle" };
        _lastSectionLabel = new Label { AutomationId = "LastSectionLabel", FontSize = 13, Text = "-" };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Storm", "StormButton", () => StartStorm(withScroll: false)),
                                 MakeButton("Storm+scroll", "StormScrollButton", () => StartStorm(withScroll: true)),
                                 MakeButton("First", "ScrollToFirstButton", () => ScrollToSection(0)),
                                 MakeButton("Last", "ScrollToLastButton", () => ScrollToSection(_sections.Count - 1)),
                                 _statusLabel,
                                 _lastSectionLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll, 0, 1);

        Content = grid;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void ScrollToSection(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        _virtualScroll.ScrollTo(index, -1, ScrollToPosition.Start, animated: false);
        _lastSectionLabel.Text = $"SH {_sections[index].Name}";
    }

    private void StartStorm(bool withScroll)
    {
        _statusLabel.Text = "Running";
        _rng = new DeterministicRandom(54321);

        var ticks = 0;
        var mutationTimer = Dispatcher.CreateTimer();
        var scrollTimer = Dispatcher.CreateTimer();

        mutationTimer.Interval = _tickInterval;
        mutationTimer.Tick += (_, _) =>
        {
            Mutate();

            if (++ticks >= TickCount)
            {
                mutationTimer.Stop();
                scrollTimer.Stop();
                _statusLabel.Text = $"Done S:{_sections.Count} I:{_records.Count}";
            }
        };

        if (withScroll)
        {
            var toEnd = true;
            scrollTimer.Interval = _scrollInterval;
            scrollTimer.Tick += (_, _) =>
            {
                var lastSection = _sections.Count - 1;

                if (lastSection < 0)
                {
                    return;
                }

                if (toEnd)
                {
                    var lastItem = Math.Max(_sections[lastSection].Items.Count - 1, 0);
                    _virtualScroll.ScrollTo(lastSection, lastItem, ScrollToPosition.End, animated: true);
                }
                else
                {
                    _virtualScroll.ScrollTo(0, 0, ScrollToPosition.Start, animated: true);
                }

                toEnd = !toEnd;
            };
            scrollTimer.Start();
        }

        mutationTimer.Start();
    }

    private void AddRecord(string groupName)
    {
        var record = new PushDataRecord($"D{++_nextId}", groupName);
        _records.Add(record);
        _cache.AddOrUpdate(record);
    }

    private List<string> GroupNames() => _records.Select(r => r.GroupName).Distinct().ToList();

    private string PickGroup(List<string> groups) => groups[_rng.Next(groups.Count)];

    private void Mutate()
    {
        var groups = GroupNames();

        switch (_rng.Next(10))
        {
            case 0 or 1 or 2:
            {
                // 1-3 records into an existing or brand-new group, one changeset.
                var group = _rng.Next(10) < 3 || groups.Count == 0 ? $"T{++_nextGroup}" : PickGroup(groups);
                var count = 1 + _rng.Next(3);
                var records = Enumerable.Range(0, count).Select(_ => new PushDataRecord($"D{++_nextId}", group)).ToArray();
                _records.AddRange(records);
                _cache.AddOrUpdate(records);

                break;
            }

            case 3 or 4:
            {
                // Remove 1-2 random records (keep a minimum population).
                var count = 1 + _rng.Next(2);

                for (var i = 0; i < count && _records.Count > 5; i++)
                {
                    var victim = _records[_rng.Next(_records.Count)];
                    _records.Remove(victim);
                    _cache.Remove(victim.Id);
                }

                break;
            }

            case 5:
            {
                // 30 records across 3 new groups in ONE changeset (Bind escalates to Reset).
                var records = Enumerable.Range(0, 30)
                                        .Select(i => new PushDataRecord($"D{++_nextId}", $"T{_nextGroup + 1 + (i % 3)}"))
                                        .ToArray();
                _nextGroup += 3;
                _records.AddRange(records);
                _cache.AddOrUpdate(records);

                break;
            }

            case 6:
            {
                // 30 records into ONE existing group in one changeset (inner Bind Reset).
                var group = groups.Count > 0 ? PickGroup(groups) : $"T{++_nextGroup}";
                var records = Enumerable.Range(0, 30).Select(_ => new PushDataRecord($"D{++_nextId}", group)).ToArray();
                _records.AddRange(records);
                _cache.AddOrUpdate(records);

                break;
            }

            case 7:
            {
                // Re-group a random record (may empty and remove its old group).
                if (_records.Count > 0 && groups.Count > 0)
                {
                    var index = _rng.Next(_records.Count);
                    var moved = _records[index] with { GroupName = PickGroup(groups) };
                    _records[index] = moved;
                    _cache.AddOrUpdate(moved);
                }

                break;
            }

            case 8:
            {
                // Drop a whole group (all its records) in one changeset.
                if (groups.Count >= 2)
                {
                    var group = PickGroup(groups);
                    var victims = _records.Where(r => r.GroupName == group).ToList();
                    _records.RemoveAll(r => r.GroupName == group);
                    _cache.Remove(victims.Select(v => v.Id));
                }

                break;
            }

            case 9:
                AddRecord($"T{++_nextGroup}");

                break;
        }
    }
}
