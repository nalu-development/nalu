using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll Refresh Tests")]
public class VirtualScrollRefreshTests : ContentPage
{
    private readonly ObservableCollection<VirtualScrollListItem> _items;
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _commandCountLabel;
    private readonly Label _eventCountLabel;
    private readonly Label _nativeStateLabel;
    private int _commandCount;
    private int _eventCount;
    private Action? _pendingCompletion;

    public VirtualScrollRefreshTests()
    {
        _items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 30).Select(i => new VirtualScrollListItem($"R{i}"))
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "RefreshScroll",
                             ItemsSource = _items,
                             IsRefreshEnabled = true,
                             RefreshAccentColor = Colors.Purple,

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label { FontSize = 16, Margin = new Thickness(16, 10) };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     return label;
                                 }
                             )
                         };

        // The refresh completion callback is delivered as the command parameter:
        // store it so a test can complete the refresh deterministically.
        _virtualScroll.RefreshCommand = new Command<Action>(complete =>
            {
                _commandCount++;
                _pendingCompletion = complete;
                UpdateLabels();
            }
        );

        _virtualScroll.OnRefresh += (_, e) =>
        {
            _eventCount++;
            _pendingCompletion = e.Complete;
            UpdateLabels();
        };

        _commandCountLabel = new Label { AutomationId = "RefreshCommandCountLabel", FontSize = 14 };
        _eventCountLabel = new Label { AutomationId = "RefreshEventCountLabel", FontSize = 14 };
        _nativeStateLabel = new Label { AutomationId = "NativeStateLabel", FontSize = 14, Text = "-" };

        var isRefreshingLabel = new Label { AutomationId = "IsRefreshingLabel", FontSize = 14 };
        isRefreshingLabel.SetBinding(Label.TextProperty, new Binding(nameof(VirtualScroll.IsRefreshing), source: _virtualScroll, stringFormat: "Refreshing: {0}"));

        UpdateLabels();

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Start refresh", "StartRefreshButton", () => _virtualScroll.IsRefreshing = true),
                                 // Invokes the same pipeline the platform uses when the user pulls to refresh
                                 // (DevFlow synthetic swipes cannot trigger the native UIRefreshControl).
                                 MakeButton("Trigger refresh", "TriggerRefreshButton", () => ((IVirtualScrollController) _virtualScroll).Refresh(() => { })),
                                 MakeButton("Complete refresh", "CompleteRefreshButton", CompleteRefresh),
                                 // Fires the native refresh control's ValueChanged action — the exact
                                 // event a physical pull gesture fires (DevFlow cannot synthesize the
                                 // pull physics themselves).
                                 MakeButton("Native pull", "NativePullButton", NativePull),
                                 MakeButton("Toggle enabled", "ToggleRefreshEnabledButton", ToggleRefreshEnabled),
                                 MakeButton("Read native", "ReadNativeStateButton", ReadNativeState),
                                 _commandCountLabel,
                                 _eventCountLabel,
                                 isRefreshingLabel,
                                 _nativeStateLabel
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
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void UpdateLabels()
    {
        _commandCountLabel.Text = $"Command: {_commandCount}";
        _eventCountLabel.Text = $"Event: {_eventCount}";
    }

    private void NativePull()
    {
#if IOS
        if (_virtualScroll.Handler is VirtualScrollHandler handler
            && handler.PlatformCollectionView.RefreshControl is { } refreshControl)
        {
            // Replicate a physical pull by firing ValueChanged — the exact action the gesture
            // fires. Deliberately NO BeginRefreshing here: the refresh pipeline sets
            // IsRefreshing, whose mapper reveals the spinner VISIBLY (BeginRefreshing alone
            // renders nothing, which would make the test pass without any real animation).
            refreshControl.SendActionForControlEvents(UIKit.UIControlEvent.ValueChanged);
            ReadNativeState();

            return;
        }
#elif ANDROID
        if (FindSwipeRefreshLayout() is { Enabled: true } swipeRefreshLayout)
        {
            // Replicate a physical pull: SwipeRefreshLayout exposes no public way to invoke
            // its OnRefreshListener, so mirror what the gesture does (start the native
            // spinner) and what the handler's listener does (run the refresh pipeline).
            swipeRefreshLayout.Refreshing = true;
            ((IVirtualScrollController) _virtualScroll).Refresh(() => { });
            ReadNativeState();

            return;
        }
#endif
        _nativeStateLabel.Text = "attached:False refreshing:False";
    }

    private void ToggleRefreshEnabled()
    {
        _virtualScroll.IsRefreshEnabled = !_virtualScroll.IsRefreshEnabled;
        ReadNativeState();
    }

    private void ReadNativeState()
    {
#if IOS
        if (_virtualScroll.Handler is VirtualScrollHandler handler)
        {
            // "attached" means a pull is currently possible.
            var refreshControl = handler.PlatformCollectionView.RefreshControl;
            _nativeStateLabel.Text = $"attached:{refreshControl is not null} refreshing:{refreshControl?.Refreshing ?? false}";

            return;
        }
#elif ANDROID
        if (FindSwipeRefreshLayout() is { } swipeRefreshLayout)
        {
            // "attached" means a pull is currently possible (Android disables rather than detaches).
            _nativeStateLabel.Text = $"attached:{swipeRefreshLayout.Enabled} refreshing:{swipeRefreshLayout.Refreshing}";

            return;
        }
#endif
        _nativeStateLabel.Text = "unsupported";
    }

#if ANDROID
    private AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout? FindSwipeRefreshLayout()
        => _virtualScroll.Handler?.PlatformView is Android.Views.View platformView
            ? FindSwipeRefreshLayout(platformView)
            : null;

    private static AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout? FindSwipeRefreshLayout(Android.Views.View view)
    {
        if (view is AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout swipeRefreshLayout)
        {
            return swipeRefreshLayout;
        }

        if (view is Android.Views.ViewGroup viewGroup)
        {
            for (var i = 0; i < viewGroup.ChildCount; i++)
            {
                if (viewGroup.GetChildAt(i) is { } child && FindSwipeRefreshLayout(child) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }
#endif

    private void CompleteRefresh()
    {
        var completion = _pendingCompletion;
        _pendingCompletion = null;
        completion?.Invoke();

        // Also stop a purely-programmatic indicator (StartRefreshButton) that never went
        // through the platform refresh pipeline.
        _virtualScroll.IsRefreshing = false;
    }
}
