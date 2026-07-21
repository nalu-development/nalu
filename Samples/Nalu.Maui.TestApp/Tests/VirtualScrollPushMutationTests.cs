using System.Collections.ObjectModel;
using DynamicData;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll Push Mutation Tests")]
public class VirtualScrollPushMutationTestsNavigationPage : NavigationPage
{
    public VirtualScrollPushMutationTestsNavigationPage() : base(new VirtualScrollPushMutationController())
    {
    }
}

/// <summary>
/// Regression harness for crashes observed when an ObservableCollection bound to a
/// VirtualScroll is mutated while the page hosting it is being pushed (or popped):
/// the platform view receives change sets while the navigation animation is still running.
/// </summary>
public class VirtualScrollPushMutationController : ContentPage
{
    private const int TickCount = 15;
    private static readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(40);

    private readonly Label _rootStatusLabel;
    private readonly Label _mutationDoneLabel;

    public VirtualScrollPushMutationController()
    {
        _rootStatusLabel = new Label { AutomationId = "RootStatusLabel", FontSize = 14, Text = "Idle" };
        _mutationDoneLabel = new Label { AutomationId = "MutationDoneLabel", FontSize = 14, Text = "-" };

        var stack = new VerticalStackLayout { Spacing = 8, Padding = 16 };
        stack.Add(MakeButton("Push + add", "PushAddButton", () => PushAndMutate("add")));
        stack.Add(MakeButton("Push + insert top", "PushInsertTopButton", () => PushAndMutate("insertTop")));
        stack.Add(MakeButton("Push + remove", "PushRemoveButton", () => PushAndMutate("remove")));
        stack.Add(MakeButton("Push + clear", "PushClearButton", () => PushAndMutate("clear")));
        stack.Add(MakeButton("Push + mixed", "PushMixedButton", () => PushAndMutate("mixed")));
        stack.Add(MakeButton("Push + pop while mutating", "PushPopButton", () => PushAndMutate("mixed", popWhileMutating: true)));
        stack.Add(MakeButton("Push grouped + add sections", "PushGroupAddButton", () => PushGroupedAndMutate("addSection")));
        stack.Add(MakeButton("Push grouped + burst sections", "PushGroupBurstButton", () => PushGroupedAndMutate("burst2")));
        stack.Add(MakeButton("Push grouped + burst & items", "PushGroupBurstItemsButton", () => PushGroupedAndMutate("burst3AndItems")));
        stack.Add(MakeButton("Push grouped + add & remove", "PushGroupAddRemoveButton", () => PushGroupedAndMutate("addRemove")));
        stack.Add(MakeButton("Push DynamicData + bursts", "PushDDBurstButton", () => PushDynamicDataAndMutate("ddBurst")));
        stack.Add(MakeButton("Push DynamicData + resets", "PushDDResetButton", () => PushDynamicDataAndMutate("ddReset")));
        stack.Add(MakeButton("Push DynamicData + regroup", "PushDDMoveButton", () => PushDynamicDataAndMutate("ddMove")));
        stack.Add(_rootStatusLabel);
        stack.Add(_mutationDoneLabel);

        Content = new ScrollView { Content = stack };
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

#pragma warning disable VSTHRD100 // async void event handler
    private async void PushAndMutate(string scenario, bool popWhileMutating = false)
#pragma warning restore VSTHRD100
    {
        _rootStatusLabel.Text = "Pushing";
        _mutationDoneLabel.Text = "-";

        var page = new MutatingVirtualScrollPage();
        var mutator = new CollectionMutator(page.Items, scenario);

        // Mutate the collection on the UI thread WHILE the push animation is running:
        // the timer owner is this (root) page so mutations keep going even when the
        // pushed page is popped mid-flight.
        var ticks = 0;
        var timer = Dispatcher.CreateTimer();
        timer.Interval = _tickInterval;
        timer.Tick += (_, _) =>
        {
            mutator.Tick(ticks++);

            if (ticks >= TickCount)
            {
                timer.Stop();
                var doneText = $"Done {page.Items.Count}";
                page.SetStatus(doneText);
                _mutationDoneLabel.Text = doneText;
            }
        };
        timer.Start();

        await Navigation.PushAsync(page, animated: true);
        _rootStatusLabel.Text = "Pushed";

        if (popWhileMutating)
        {
            // Pop immediately: the remaining mutation ticks land while the pop
            // animation runs and after the page is gone.
            await Navigation.PopAsync(animated: true);
            _rootStatusLabel.Text = "Popped";
        }
    }

#pragma warning disable VSTHRD100 // async void event handler
    private async void PushGroupedAndMutate(string scenario)
#pragma warning restore VSTHRD100
    {
        _rootStatusLabel.Text = "Pushing";
        _mutationDoneLabel.Text = "-";

        var page = new MutatingGroupedVirtualScrollPage();
        var mutator = new GroupedCollectionMutator(page.Sections, scenario);

        var ticks = 0;
        var timer = Dispatcher.CreateTimer();
        timer.Interval = _tickInterval;
        timer.Tick += (_, _) =>
        {
            // Each tick applies MULTIPLE changes synchronously (bursts): several
            // CollectionChanged notifications reach the adapter in the same main-loop
            // pass, while the push animation is still running.
            mutator.Tick(ticks++);

            if (ticks >= TickCount)
            {
                timer.Stop();
                var doneText = $"Done {page.Sections.Count}";
                page.SetStatus(doneText);
                _mutationDoneLabel.Text = doneText;
            }
        };
        timer.Start();

        await Navigation.PushAsync(page, animated: true);
        _rootStatusLabel.Text = "Pushed";
    }

#pragma warning disable VSTHRD100 // async void event handler
    private async void PushDynamicDataAndMutate(string scenario)
#pragma warning restore VSTHRD100
    {
        _rootStatusLabel.Text = "Pushing";
        _mutationDoneLabel.Text = "-";

        var page = new MutatingDynamicDataGroupedPage();
        var mutator = new DynamicDataMutator(page.Cache);

        var ticks = 0;
        var timer = Dispatcher.CreateTimer();
        timer.Interval = _tickInterval;
        timer.Tick += (_, _) =>
        {
            // Every AddOrUpdate call emits one DynamicData changeset that fans out
            // synchronously through Group/Transform/Bind on this same main-loop pass:
            // the outer AND the inner ReadOnlyObservableCollections all mutate at once,
            // with Bind escalating large changesets (>25) to a Reset notification.
            mutator.Tick(scenario, ticks++);

            if (ticks >= TickCount)
            {
                timer.Stop();
                var doneText = $"Done {page.Sections.Count}";
                page.SetStatus(doneText);
                _mutationDoneLabel.Text = doneText;
            }
        };
        timer.Start();

        await Navigation.PushAsync(page, animated: true);
        _rootStatusLabel.Text = "Pushed";
    }

    private sealed class DynamicDataMutator(SourceCache<PushDataRecord, string> cache)
    {
        private int _next;

        private PushDataRecord NewRecord(string groupName) => new($"N{++_next}", groupName);

        public void Tick(string scenario, int tick)
        {
            switch (scenario)
            {
                case "ddBurst":
                    // 6 items spanning 2 NEW groups in a single changeset.
                    cache.AddOrUpdate(new[]
                        {
                            NewRecord($"B{tick}a"), NewRecord($"B{tick}a"), NewRecord($"B{tick}a"),
                            NewRecord($"B{tick}b"), NewRecord($"B{tick}b"), NewRecord($"B{tick}b")
                        }
                    );

                    break;

                case "ddReset":
                    switch (tick)
                    {
                        case 0:
                            // 30 items into one NEW group: the section arrives pre-populated.
                            cache.AddOrUpdate(Enumerable.Range(0, 30).Select(_ => NewRecord("DBIG")).ToArray());

                            break;

                        case 3:
                            // 30 more items into the EXISTING group: the inner Bind escalates to Reset.
                            cache.AddOrUpdate(Enumerable.Range(0, 30).Select(_ => NewRecord("DBIG")).ToArray());

                            break;

                        case 6:
                            // 30 items each in its own NEW group: the outer Bind escalates to Reset.
                            cache.AddOrUpdate(Enumerable.Range(0, 30).Select(i => NewRecord($"R{i}")).ToArray());

                            break;

                        default:
                            cache.AddOrUpdate(NewRecord($"S{tick}"));

                            break;
                    }

                    break;

                case "ddMove":
                    switch (tick)
                    {
                        case < 5:
                            // Re-group seed items into "DM" (D1 empties and its group vanishes).
                            cache.AddOrUpdate(new PushDataRecord($"I{tick + 1}", "DM"));

                            break;

                        case < 10:
                            // Remove the re-grouped items ("DM" empties and vanishes).
                            cache.Remove($"I{tick - 4}");

                            break;

                        default:
                            cache.AddOrUpdate(NewRecord($"M{tick}"));

                            break;
                    }

                    break;
            }
        }
    }

    private sealed class GroupedCollectionMutator(ObservableCollection<VirtualScrollSection> sections, string scenario)
    {
        private int _nextSection = 3;

        private VirtualScrollSection NewSection()
        {
            var name = $"G{++_nextSection}";

            return new VirtualScrollSection(name, Enumerable.Range(1, 3).Select(i => new VirtualScrollListItem($"{name}i{i}")));
        }

        public void Tick(int tick)
        {
            switch (scenario)
            {
                case "addSection":
                    sections.Add(NewSection());

                    break;

                case "burst2":
                    // Two section adds back-to-back in the same main-loop pass.
                    sections.Add(NewSection());
                    sections.Add(NewSection());

                    break;

                case "burst3AndItems":
                    // Three section adds plus item-level adds, all in the same pass.
                    sections.Add(NewSection());
                    sections.Add(NewSection());
                    sections.Add(NewSection());
                    sections[0].Items.Add(new VirtualScrollListItem($"G1x{tick}"));
                    sections[^1].Items.Add(new VirtualScrollListItem($"tailx{tick}"));

                    break;

                case "addRemove":
                    // Add two sections and remove the first one in the same pass.
                    sections.Add(NewSection());
                    sections.Add(NewSection());

                    if (sections.Count > 1)
                    {
                        sections.RemoveAt(0);
                    }

                    break;
            }
        }
    }

    private sealed class CollectionMutator(ObservableCollection<VirtualScrollListItem> items, string scenario)
    {
        private int _next = 20;

        public void Tick(int tick)
        {
            switch (scenario)
            {
                case "add":
                    items.Add(new VirtualScrollListItem($"P{++_next}"));

                    break;

                case "insertTop":
                    items.Insert(0, new VirtualScrollListItem($"P{++_next}"));

                    break;

                case "remove":
                    if (items.Count > 0)
                    {
                        items.RemoveAt(0);
                    }

                    break;

                case "clear":
                    if (tick == 5)
                    {
                        items.Clear();
                    }
                    else
                    {
                        items.Add(new VirtualScrollListItem($"P{++_next}"));
                    }

                    break;

                case "mixed":
                    switch (tick % 5)
                    {
                        case 0:
                            items.Add(new VirtualScrollListItem($"P{++_next}"));

                            break;

                        case 1:
                            items.Insert(0, new VirtualScrollListItem($"P{++_next}"));

                            break;

                        case 2:
                            if (items.Count > 0)
                            {
                                items.RemoveAt(0);
                            }

                            break;

                        case 3:
                            if (items.Count > 1)
                            {
                                items.Move(0, items.Count - 1);
                            }

                            break;

                        case 4:
                            if (items.Count > 0)
                            {
                                items[0] = new VirtualScrollListItem($"P{++_next}");
                            }

                            break;
                    }

                    break;
            }
        }
    }
}

public record PushDataRecord(string Id, string GroupName);

/// <summary>
/// A section view-model fed by a DynamicData group: its own Bind writes into the inner
/// ReadOnlyObservableCollection, mirroring real-world SourceCache + Group pipelines.
/// </summary>
public sealed class DynamicDataSection : IDisposable
{
    private readonly ReadOnlyObservableCollection<VirtualScrollListItem> _items;
    private readonly IDisposable _subscription;

    public string Name { get; }
    public ReadOnlyObservableCollection<VirtualScrollListItem> Items => _items;

    public DynamicDataSection(IGroup<PushDataRecord, string, string> group)
    {
        Name = group.Key;
        _subscription = group.Cache.Connect()
                             .Transform(r => new VirtualScrollListItem(r.Id))
                             .Bind(out _items)
                             .Subscribe();
    }

    public void Dispose() => _subscription.Dispose();
}

/// <summary>
/// Reproduces the DynamicData shape that crashed in the field: SourceCache → Group →
/// Transform(section with its own inner Bind) → Bind(out ReadOnlyObservableCollection),
/// consumed through the grouped NotifyCollectionChanged adapter, mutated during push.
/// </summary>
public class MutatingDynamicDataGroupedPage : ContentPage
{
    private readonly Label _statusLabel;
    private readonly IDisposable _subscription;
    private readonly ReadOnlyObservableCollection<DynamicDataSection> _sections;

    public SourceCache<PushDataRecord, string> Cache { get; } = new(r => r.Id);

    public ReadOnlyObservableCollection<DynamicDataSection> Sections => _sections;

    public MutatingDynamicDataGroupedPage()
    {
        // Seed: I1-I3 → D1, I4-I6 → D2, I7-I9 → D3.
        Cache.AddOrUpdate(Enumerable.Range(1, 9).Select(i => new PushDataRecord($"I{i}", $"D{((i - 1) / 3) + 1}")));

        _subscription = Cache.Connect()
                             .Group(r => r.GroupName)
                             .Transform(g => new DynamicDataSection(g))
                             .DisposeMany()
                             .Bind(out _sections)
                             .Subscribe();

        _statusLabel = new Label { AutomationId = "MutationStatusLabel", FontSize = 14, Text = "Running", Padding = new Thickness(16, 8) };

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "PushDDScroll",
                                ItemsSource = VirtualScroll.CreateObservableCollectionAdapter(_sections, s => s.Items),

                                HeaderTemplate = new DataTemplate(() => new Label
                                    {
                                        AutomationId = "PushDDHeader",
                                        Text = "DynamicData push header",
                                        FontAttributes = FontAttributes.Bold,
                                        HorizontalOptions = LayoutOptions.Center
                                    }
                                ),

                                SectionHeaderTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label
                                                    {
                                                        FontSize = 16,
                                                        FontAttributes = FontAttributes.Bold,
                                                        BackgroundColor = Colors.LightGray,
                                                        Padding = new Thickness(10, 4)
                                                    };
                                        label.SetBinding(Label.TextProperty, nameof(DynamicDataSection.Name));
                                        label.SetBinding(AutomationIdProperty, new Binding(nameof(DynamicDataSection.Name), stringFormat: "DD {0}"));

                                        return label;
                                    }
                                ),

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label { FontSize = 14, Margin = new Thickness(16, 6) };
                                        label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                        label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                        return label;
                                    }
                                )
                            };

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(_statusLabel);
        grid.Add(virtualScroll, 0, 1);

        Content = grid;
    }

    public void SetStatus(string text) => _statusLabel.Text = text;
}

public class MutatingGroupedVirtualScrollPage : ContentPage
{
    private readonly Label _statusLabel;

    public ObservableCollection<VirtualScrollSection> Sections { get; } = new(
        new[] { "G1", "G2", "G3" }.Select(name =>
            new VirtualScrollSection(name, Enumerable.Range(1, 3).Select(i => new VirtualScrollListItem($"{name}i{i}"))))
    );

    public MutatingGroupedVirtualScrollPage()
    {
        _statusLabel = new Label { AutomationId = "MutationStatusLabel", FontSize = 14, Text = "Running", Padding = new Thickness(16, 8) };

        var adapter = VirtualScroll.CreateObservableCollectionAdapter(Sections, s => s.Items);

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "PushGroupedScroll",
                                ItemsSource = adapter,

                                HeaderTemplate = new DataTemplate(() => new Label
                                    {
                                        AutomationId = "PushGroupedHeader",
                                        Text = "Grouped push header",
                                        FontAttributes = FontAttributes.Bold,
                                        HorizontalOptions = LayoutOptions.Center
                                    }
                                ),

                                SectionHeaderTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label
                                                    {
                                                        FontSize = 16,
                                                        FontAttributes = FontAttributes.Bold,
                                                        BackgroundColor = Colors.LightGray,
                                                        Padding = new Thickness(10, 4)
                                                    };
                                        label.SetBinding(Label.TextProperty, nameof(VirtualScrollSection.Name));
                                        label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "PH {0}"));

                                        return label;
                                    }
                                ),

                                SectionFooterTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label { FontSize = 12, FontAttributes = FontAttributes.Italic, Padding = new Thickness(10, 2) };
                                        label.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "end {0}"));
                                        label.SetBinding(AutomationIdProperty, new Binding(nameof(VirtualScrollSection.Name), stringFormat: "PF {0}"));

                                        return label;
                                    }
                                ),

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label { FontSize = 14, Margin = new Thickness(16, 6) };
                                        label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                        label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                        return label;
                                    }
                                )
                            };

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(_statusLabel);
        grid.Add(virtualScroll, 0, 1);

        Content = grid;
    }

    public void SetStatus(string text) => _statusLabel.Text = text;
}

public class MutatingVirtualScrollPage : ContentPage
{
    private readonly Label _statusLabel;

    public ObservableCollection<VirtualScrollListItem> Items { get; } = new(
        Enumerable.Range(1, 20).Select(i => new VirtualScrollListItem($"P{i}"))
    );

    public MutatingVirtualScrollPage()
    {
        _statusLabel = new Label { AutomationId = "MutationStatusLabel", FontSize = 14, Text = "Running", Padding = new Thickness(16, 8) };

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "PushScroll",
                                ItemsSource = Items,

                                HeaderTemplate = new DataTemplate(() => new Label
                                    {
                                        AutomationId = "PushHeader",
                                        Text = "Push header",
                                        FontAttributes = FontAttributes.Bold,
                                        HorizontalOptions = LayoutOptions.Center
                                    }
                                ),

                                FooterTemplate = new DataTemplate(() => new Label
                                    {
                                        AutomationId = "PushFooter",
                                        Text = "Push footer",
                                        FontAttributes = FontAttributes.Italic,
                                        HorizontalOptions = LayoutOptions.Center
                                    }
                                ),

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label { FontSize = 16, Margin = new Thickness(16, 8) };
                                        label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                        label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                        return label;
                                    }
                                )
                            };

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(_statusLabel);
        grid.Add(virtualScroll, 0, 1);

        Content = grid;
    }

    public void SetStatus(string text) => _statusLabel.Text = text;
}
