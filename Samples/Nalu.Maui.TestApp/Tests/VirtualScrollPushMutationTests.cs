using System.Collections.ObjectModel;
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
