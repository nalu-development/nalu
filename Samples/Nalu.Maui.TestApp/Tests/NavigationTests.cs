using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Global, compact navigation event log asserted by the UI tests.
/// Entry format: "{Page}+E" entering, "{Page}+A" appearing, "{Page}-D" disappearing,
/// "{Page}-L" leaving, "{Page}-X" disposed, "{Page}?G{y|n}" guard evaluated,
/// "{Page}:I{n}" intent received, "{Page}:R={v}" awaitable result received.
/// </summary>
internal static class NavLog
{
    private static readonly List<string> _entries = [];

    public static event Action? Changed;

    public static string Text => string.Join(",", _entries);

    public static void Add(string entry)
    {
        _entries.Add(entry);
        Changed?.Invoke();
    }

    public static void Clear()
    {
        _entries.Clear();
        Changed?.Invoke();
    }
}

public record ProductIntent(int ProductId);

public class PickValueIntent : AwaitableIntent<string>;

/// <summary>Base page model logging every navigation lifecycle event.</summary>
public abstract class NavPageModelBase : ObservableObject, IEnteringAware, IAppearingAware, IDisappearingAware, ILeavingAware, IDisposable
{
    protected abstract string Name { get; }

    protected INavigationService NavigationService { get; }

    public string LogText => NavLog.Text;

    protected NavPageModelBase(INavigationService navigationService)
    {
        NavigationService = navigationService;
        NavLog.Changed += OnLogChanged;
    }

    private void OnLogChanged() => OnPropertyChanged(nameof(LogText));

    public virtual ValueTask OnEnteringAsync()
    {
        NavLog.Add($"{Name}+E");

        return ValueTask.CompletedTask;
    }

    public ValueTask OnAppearingAsync()
    {
        NavLog.Add($"{Name}+A");

        return ValueTask.CompletedTask;
    }

    public ValueTask OnDisappearingAsync()
    {
        NavLog.Add($"{Name}-D");

        return ValueTask.CompletedTask;
    }

    public ValueTask OnLeavingAsync()
    {
        NavLog.Add($"{Name}-L");

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // A model disposed by a navigation must become collectable (its page holds it via
        // BindingContext, so this also asserts the page itself doesn't leak).
        LeakTracker.ExpectCollected(this);
        NavLog.Add($"{Name}-X");
        NavLog.Changed -= OnLogChanged;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Root of the first tab: launches pushes, intents and absolute navigations.</summary>
public partial class NavHomePageModel(INavigationService navigationService) : NavPageModelBase(navigationService)
{
    protected override string Name => "Home";

    [ObservableProperty]
    private string _resolvedValue = "-";

    public Task PushDetail() => NavigationService.GoToAsync(Navigation.Relative().Push<NavDetailPageModel>());

    public Task PushDetailWithIntent() => NavigationService.GoToAsync(Navigation.Relative().Push<NavDetailPageModel>().WithIntent(new ProductIntent(42)));

    public async Task ResolvePick()
    {
        var value = await NavigationService.ResolveIntentAsync<NavDetailPageModel, string>(new PickValueIntent());
        ResolvedValue = value;
        NavLog.Add($"Home:R={value}");
    }

    public Task GoSearchRoot() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavSearchPageModel>());

    public Task GoSettingsRoot() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavSettingsPageModel>());

    public Task GoSearchRootAddEditor() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavSearchPageModel>().Add<NavEditorPageModel>());
}

/// <summary>Pushed page: receives intents and returns awaitable results.</summary>
public partial class NavDetailPageModel(INavigationService navigationService) : NavPageModelBase(navigationService), IEnteringAware<ProductIntent>, IEnteringAware<PickValueIntent>
{
    private PickValueIntent? _pickIntent;

    protected override string Name => "Detail";

    [ObservableProperty]
    private string _receivedIntent = "-";

    public ValueTask OnEnteringAsync(ProductIntent intent)
    {
        ReceivedIntent = intent.ProductId.ToString();
        NavLog.Add($"Detail:I{intent.ProductId}");

        return OnEnteringAsync();
    }

    public ValueTask OnEnteringAsync(PickValueIntent intent)
    {
        _pickIntent = intent;
        ReceivedIntent = "pick";
        NavLog.Add("Detail:AI");

        return OnEnteringAsync();
    }

    public Task Pop() => NavigationService.GoToAsync(Navigation.Relative().Pop());

    public Task ReplaceWithEditor() => NavigationService.GoToAsync(Navigation.Relative().Pop().Push<NavEditorPageModel>());

    public Task PushEditor() => NavigationService.GoToAsync(Navigation.Relative().Push<NavEditorPageModel>());

    public async Task SetResultAndPop()
    {
        _pickIntent?.SetResult("picked");
        await NavigationService.GoToAsync(Navigation.Relative().Pop());
    }

    public Task GoSettingsRoot() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavSettingsPageModel>());
}

/// <summary>Root of the second tab (same ShellItem as Home).</summary>
public partial class NavSearchPageModel(INavigationService navigationService) : NavPageModelBase(navigationService)
{
    protected override string Name => "Search";

    public Task PushEditor() => NavigationService.GoToAsync(Navigation.Relative().Push<NavEditorPageModel>());

    public Task GoHomeRoot() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavHomePageModel>());
}

/// <summary>Guarded page: simulates "unsaved changes" via ILeavingGuard.</summary>
public partial class NavEditorPageModel(INavigationService navigationService) : NavPageModelBase(navigationService), ILeavingGuard
{
    protected override string Name => "Editor";

    [ObservableProperty]
    private bool _canLeave;

    public ValueTask<bool> CanLeaveAsync()
    {
        NavLog.Add($"Editor?G{(CanLeave ? "y" : "n")}");

        return ValueTask.FromResult(CanLeave);
    }

    public Task Pop() => NavigationService.GoToAsync(Navigation.Relative().Pop());

    public Task PopIgnoringGuards() => NavigationService.GoToAsync(Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop());

    public Task ReplaceWithDetailIgnoringGuards() => NavigationService.GoToAsync(Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop().Push<NavDetailPageModel>());

    public Task GoHomeRootIgnoringGuards() => NavigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.IgnoreGuards).Root<NavHomePageModel>());

    public Task GoSettingsRoot() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavSettingsPageModel>());

    public void ToggleGuard() => CanLeave = !CanLeave;
}

/// <summary>Root of a separate ShellItem: switching to it clears the other item's stacks.</summary>
public partial class NavSettingsPageModel(INavigationService navigationService) : NavPageModelBase(navigationService)
{
    protected override string Name => "Settings";

    public Task GoHomeRoot() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavHomePageModel>());

    public Task GoHomeRootAddDetail() => NavigationService.GoToAsync(Navigation.Absolute().Root<NavHomePageModel>().Add<NavDetailPageModel>());
}

internal static class NavPageFactory
{
    public static Button MakeButton(string text, string automationId, Func<Task> action)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                NavLog.Add($"ERR:{ex.GetType().Name}({ex.Message.Replace(',', ';')})");
            }
        };

        return button;
    }

    public static View BuildContent(string name, params View[] extraViews)
    {
        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };

        stack.Add(new Label { Text = $"Nav {name}", AutomationId = $"NavPage{name}", FontSize = 22, FontAttributes = FontAttributes.Bold });

        var logLabel = new Label { AutomationId = $"Log{name}", FontSize = 11, LineBreakMode = LineBreakMode.CharacterWrap };
        logLabel.SetBinding(Label.TextProperty, nameof(NavPageModelBase.LogText));
        stack.Add(logLabel);

        var clearButton = new Button { Text = "Clear log", AutomationId = $"ClearLog{name}", FontSize = 11 };
        clearButton.Clicked += (_, _) => NavLog.Clear();
        stack.Add(clearButton);

        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{name}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        foreach (var view in extraViews)
        {
            stack.Add(view);
        }

        return new ScrollView { Content = stack };
    }
}

[UsedImplicitly]
public class NavHomePage : ContentPage
{
    public NavHomePage(NavHomePageModel model)
    {
        var resolvedLabel = new Label { AutomationId = "ResolvedLabel", FontSize = 14 };
        resolvedLabel.SetBinding(Label.TextProperty, nameof(NavHomePageModel.ResolvedValue));

        var inner = NavPageFactory.BuildContent(
            "Home",
            resolvedLabel,
            NavPageFactory.MakeButton("Push Detail", "PushDetailButton", model.PushDetail),
            NavPageFactory.MakeButton("Push Detail + intent", "PushDetailIntentButton", model.PushDetailWithIntent),
            NavPageFactory.MakeButton("Resolve pick", "ResolvePickButton", model.ResolvePick),
            NavPageFactory.MakeButton("Go Search root", "GoSearchRootButton", model.GoSearchRoot),
            NavPageFactory.MakeButton("Go Settings root", "GoSettingsRootButton", model.GoSettingsRoot),
            NavPageFactory.MakeButton("Go Search + Editor", "GoSearchAddEditorButton", model.GoSearchRootAddEditor)
        );

        Title = "Home";
        BindingContext = model;
        Content = inner;
    }
}

[UsedImplicitly]
public class NavDetailPage : ContentPage
{
    public NavDetailPage(NavDetailPageModel model)
    {
        var intentLabel = new Label { AutomationId = "DetailIntentLabel", FontSize = 14 };
        intentLabel.SetBinding(Label.TextProperty, nameof(NavDetailPageModel.ReceivedIntent));

        var inner = NavPageFactory.BuildContent(
            "Detail",
            intentLabel,
            NavPageFactory.MakeButton("Pop", "PopDetailButton", model.Pop),
            NavPageFactory.MakeButton("Replace with Editor", "ReplaceWithEditorButton", model.ReplaceWithEditor),
            NavPageFactory.MakeButton("Push Editor", "PushEditorButton", model.PushEditor),
            NavPageFactory.MakeButton("Set result + pop", "SetResultButton", model.SetResultAndPop),
            NavPageFactory.MakeButton("Go Settings root", "GoSettingsFromDetailButton", model.GoSettingsRoot)
        );

        Title = "Detail";
        BindingContext = model;
        Content = inner;
    }
}

[UsedImplicitly]
public class NavSearchPage : ContentPage
{
    public NavSearchPage(NavSearchPageModel model)
    {
        var inner = NavPageFactory.BuildContent(
            "Search",
            NavPageFactory.MakeButton("Push Editor", "PushEditorFromSearchButton", model.PushEditor),
            NavPageFactory.MakeButton("Go Home root", "GoHomeRootButton", model.GoHomeRoot)
        );

        Title = "Search";
        BindingContext = model;
        Content = inner;
    }
}

[UsedImplicitly]
public class NavEditorPage : ContentPage
{
    public NavEditorPage(NavEditorPageModel model)
    {
        var guardLabel = new Label { AutomationId = "GuardStateLabel", FontSize = 14 };
        guardLabel.SetBinding(Label.TextProperty, new Binding(nameof(NavEditorPageModel.CanLeave), stringFormat: "CanLeave: {0}"));

        var toggleButton = new Button { Text = "Toggle guard", AutomationId = "ToggleGuardButton", FontSize = 11 };
        toggleButton.Clicked += (_, _) => model.ToggleGuard();

        var inner = NavPageFactory.BuildContent(
            "Editor",
            guardLabel,
            toggleButton,
            NavPageFactory.MakeButton("Pop", "PopEditorButton", model.Pop),
            NavPageFactory.MakeButton("Pop ignoring guards", "PopIgnoreGuardsButton", model.PopIgnoringGuards),
            NavPageFactory.MakeButton("Replace w/ Detail (no guards)", "ReplaceIgnoreGuardsButton", model.ReplaceWithDetailIgnoringGuards),
            NavPageFactory.MakeButton("Go Home root (no guards)", "GoHomeRootIgnoreGuardsButton", model.GoHomeRootIgnoringGuards),
            NavPageFactory.MakeButton("Go Settings root", "GoSettingsFromEditorButton", model.GoSettingsRoot)
        );

        Title = "Editor";
        BindingContext = model;
        Content = inner;
    }
}

[UsedImplicitly]
public class NavSettingsPage : ContentPage
{
    public NavSettingsPage(NavSettingsPageModel model)
    {
        var inner = NavPageFactory.BuildContent(
            "Settings",
            NavPageFactory.MakeButton("Go Home root", "GoHomeFromSettingsButton", model.GoHomeRoot),
            NavPageFactory.MakeButton("Go Home + Detail", "GoHomeAddDetailButton", model.GoHomeRootAddDetail)
        );

        Title = "Settings";
        BindingContext = model;
        Content = inner;
    }
}

/// <summary>
/// Shell harness for the navigation tests:
/// ShellItem 1 holds two sections (tabs HomeTab: Home, SearchTab: Search) — switching preserves stacks;
/// ShellItem 2 holds Settings — switching to it clears the other item's stacks.
/// </summary>
[UsedImplicitly]
[TestPage("Navigation Tests")]
public class NavShell : NaluShell
{
    public NavShell(INavigationService navigationService) : base(navigationService, typeof(NavHomePage))
    {
        NavLog.Clear();

        var homeContent = new ShellContent { Title = "Home" };
        Nalu.Navigation.SetPageType(homeContent, typeof(NavHomePage));

        var searchContent = new ShellContent { Title = "Search" };
        Nalu.Navigation.SetPageType(searchContent, typeof(NavSearchPage));

        var tabBar = new TabBar();
        var homeTab = new Tab { Title = "HomeTab" };
        homeTab.Items.Add(homeContent);
        var searchTab = new Tab { Title = "SearchTab" };
        searchTab.Items.Add(searchContent);
        tabBar.Items.Add(homeTab);
        tabBar.Items.Add(searchTab);

        // Use the Nalu custom tab bar: its tab buttons are real MAUI views (tappable by
        // UI tests) and navigate via Shell.GoToAsync, exercising the same cancelable
        // OnNavigating pipeline a native tab tap goes through.
        SetTabBarView(tabBar, new NaluTabBar());

        var settingsContent = new ShellContent { Title = "Settings" };
        Nalu.Navigation.SetPageType(settingsContent, typeof(NavSettingsPage));

        var settingsItem = new FlyoutItem { Title = "SettingsItem" };
        settingsItem.Items.Add(settingsContent);

        Items.Add(tabBar);
        Items.Add(settingsItem);

        FlyoutBehavior = FlyoutBehavior.Disabled;
    }
}
