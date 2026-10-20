using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class SrTabOnePageModel(INavigationService navigationService) : ObservableObject, IEnteringAware
{
    /// <summary>Armed by the plain page: the next Entering collects, INSIDE the area switch.</summary>
    internal static bool CollectOnEntering { get; set; }

    public Task GoPlain() => navigationService.GoToAsync(Navigation.Absolute().Root<SrPlainPageModel>());

    /// <summary>
    /// Pushes a page and pops it: the popped page's container is released at the end of the pop's
    /// own synchronization, retiring its nav bar strip — the bar host must come back unparented.
    /// </summary>
    public async Task<string> ProbeNavBarRelease(Element element)
    {
        await navigationService.GoToAsync(Navigation.Relative().Push<SrDetailPageModel>());
        var bar = ScaffoldStripReuseScaffold.CaptureNavBar(element, SrDetailPage.PageTitle);
        await navigationService.GoToAsync(Navigation.Relative().Pop());

        return ScaffoldStripReuseScaffold.DescribeSuperview(bar);
    }

    public ValueTask OnEnteringAsync()
    {
        if (CollectOnEntering)
        {
            CollectOnEntering = false;
            ScaffoldStripReuseScaffold.CollectOnMainThread();
        }

        return ValueTask.CompletedTask;
    }
}

[UsedImplicitly]
public partial class SrTabTwoPageModel : ObservableObject;

[UsedImplicitly]
public partial class SrDetailPageModel : ObservableObject;

[UsedImplicitly]
public partial class SrPlainPageModel(INavigationService navigationService) : ObservableObject
{
    public Task GoTabRoot() => navigationService.GoToAsync(Navigation.Absolute().Root<SrTabOnePageModel>());

    /// <summary>
    /// The field shape: a collection lands INSIDE the switch (there, allocations of the incoming
    /// page), after the last yield and before the strip remounts the bar. Collecting before the
    /// switch is not enough: the navigation yields first and the deferred release drains.
    /// </summary>
    public async Task<string> CollectDuringSwitchToTabRoot()
    {
        SrTabOnePageModel.CollectOnEntering = true;

        try
        {
            return await navigationService.GoToAsync(Navigation.Absolute().Root<SrTabOnePageModel>()) ? "ok" : "refused";
        }
        catch (Exception exception)
        {
            // A failed switch leaves no page on screen to show the label: the log carries the stack.
            Console.WriteLine($"[StripReuse] switch failed: {exception}");

            return $"threw={exception.GetType().Name}";
        }
        finally
        {
            SrTabOnePageModel.CollectOnEntering = false;
        }
    }
}

/// <summary>First root of the TAB BAR area (the initial area: the strip is mounted at startup).</summary>
[UsedImplicitly]
public class SrTabOnePage : ContentPage
{
    public SrTabOnePage(SrTabOnePageModel model)
    {
        BindingContext = model;
        Title = "Tab one";

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "SrTabOnePage", AutomationId = "SrTabOnePage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Go plain", "SrGoPlain", model.GoPlain));

        AddProbe(stack, "Nav bar probe", "SrNavBarProbe", async () => await model.ProbeNavBarRelease(this));
        AddProbe(stack, "Popup probe", "SrPopupProbe", async () => await ScaffoldStripReuseScaffold.ProbePopupRelease(this));
        AddProbe(stack, "Reconnect probe", "SrReconnectProbe", () => Task.FromResult(ScaffoldStripReuseScaffold.ProbeReconnect(this)));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitSrTabOne", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }

    /// <summary>A probe button and its result label (<c>{automationId}Result</c>).</summary>
    private static void AddProbe(VerticalStackLayout stack, string text, string automationId, Func<Task<string>> probe)
    {
        var result = new Label { AutomationId = $"{automationId}Result", FontSize = 14, Text = "idle" };
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += async (_, _) => result.Text = await probe();
        stack.Add(button);
        stack.Add(result);
    }
}

/// <summary>Pushed on top of tab one by the nav bar probe, popped right after.</summary>
[UsedImplicitly]
public class SrDetailPage : ContentPage
{
    internal const string PageTitle = "Sr detail";

    public SrDetailPage(SrDetailPageModel model)
    {
        BindingContext = model;
        Title = PageTitle;

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitSrDetail", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
                  {
                      Spacing = 12,
                      Padding = 16,
                      Children = { new Label { Text = "SrDetailPage", AutomationId = "SrDetailPage", FontSize = 22 }, exitButton }
                  };
    }
}

[UsedImplicitly]
public class SrTabTwoPage : ContentPage
{
    public SrTabTwoPage(SrTabTwoPageModel model)
    {
        BindingContext = model;
        Title = "Tab two";
        Content = new Label { Text = "SrTabTwoPage", AutomationId = "SrTabTwoPage", FontSize = 22 };
    }
}

/// <summary>Root of the PLAIN area: no tab bar, so arriving here unmounts the strip.</summary>
[UsedImplicitly]
public class SrPlainPage : ContentPage
{
    public SrPlainPage(SrPlainPageModel model)
    {
        BindingContext = model;
        Title = "Plain";

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "SrPlainPage", AutomationId = "SrPlainPage", FontSize = 22, FontAttributes = FontAttributes.Bold });

        var probe = new Label { AutomationId = "SrProbeResult", FontSize = 16, Text = "idle" };
        var probeButton = new Button { Text = "Collect + probe bar", AutomationId = "SrProbe", FontSize = 11 };
        probeButton.Clicked += (_, _) => probe.Text = ScaffoldStripReuseScaffold.ProbeTabBarSuperview(this);
        stack.Add(probeButton);
        stack.Add(probe);

        stack.Add(NavPageFactory.MakeButton("Go tab root", "SrGoTab", model.GoTabRoot));

        var switchResult = new Label { AutomationId = "SrSwitchResult", FontSize = 16, Text = "idle" };
        var collectDuringSwitchButton = new Button { Text = "Collect during switch to tab root", AutomationId = "SrCollectDuringSwitch", FontSize = 11 };
        collectDuringSwitchButton.Clicked += async (_, _) => switchResult.Text = await model.CollectDuringSwitchToTabRoot();
        stack.Add(collectDuringSwitchButton);
        stack.Add(switchResult);

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitSrPlain", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

/// <summary>
/// Harness for native hosts releasing what they host (see <c>ScaffoldHostView</c>). Hosted MAUI
/// views outlive their host — the tab bar area keeps its bar for the return, page hosts keep their
/// nav bar host, overlay content outlives its presentation — so a retired host must not keep them
/// parented: once retired, its managed peer is collectable while its native view lingers until a
/// deferred release on the main thread, and any managed <c>Superview</c> read from the hosted view
/// in between (the next host adopting it, MAUI's invalidation walk) throws "Failed to marshal the
/// Objective-C object … (type: Nalu_ScaffoldTabBarStrip)". The probes read the hosted view's
/// superview right after its host is retired (the tab bar probe first collects ON the main thread,
/// so the deferred release cannot drain meanwhile): <c>superview=null</c> is the invariant.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Strip Reuse Tests")]
public class ScaffoldStripReuseScaffold : Scaffold
{
    public ScaffoldStripReuseScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "One", PageType = typeof(SrTabOnePage) },
                    new ScaffoldRoot { Title = "Two", PageType = typeof(SrTabTwoPage) }
                }
            }
        );

        Areas.Add(new ScaffoldRoot { Title = "Plain", PageType = typeof(SrPlainPage) });
    }

    /// <summary>Full blocking collections: anything only weakly held (a retired strip's peer) goes now.</summary>
    internal static void CollectOnMainThread()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// Collects, then reads the hidden tab bar's native superview as the old strip constructor did:
    /// <c>superview=null</c> once the retired strip released the bar, <c>threw=…</c> when the bar
    /// is still parented to a strip whose managed peer is gone.
    /// </summary>
    internal static string ProbeTabBarSuperview(Element element)
        => DescribeSuperview(TabBarViewOf(element) is { } barView ? PlatformViewOf(barView) : null, collect: true);

    /// <summary>
    /// Shows a popup and closes it: the closed entry's panel host is retired, and the content's
    /// platform view (captured while presented) must come back unparented.
    /// </summary>
    internal static async Task<string> ProbePopupRelease(Element element)
    {
        if (FindScaffold(element) is not { } scaffold)
        {
            return "no-scaffold";
        }

        var content = new Label { Text = "probe popup", AutomationId = "SrProbePopupContent", Padding = 24, BackgroundColor = Colors.White };
        var popup = await scaffold.ShowPopupAsync(content);
        var panel = PlatformViewOf(content);
        await popup.CloseAsync();

        return DescribeSuperview(panel);
    }

    /// <summary>
    /// Disconnects ONLY the scaffold's handler (the bar keeps its own, so the next connection reuses
    /// the same bar view), reads the bar's native superview and logical parent, then reconnects. Both
    /// must be gone: the disconnect retires the strip, the disposed presenter detaches the bar.
    /// </summary>
    internal static string ProbeReconnect(Element element)
    {
        if (FindScaffold(element) is not { } scaffold || TabBarViewOf(element) is not { } barView)
        {
            return "no-bar";
        }

        var bar = PlatformViewOf(barView);
        var window = Application.Current!.Windows[0];
        window.Page = new ContentPage { Content = new Label { Text = "placeholder" } };
        scaffold.Handler = null;

        var result = $"{DescribeSuperview(bar)} parent={(barView.Parent is null ? "null" : "set")}";
        scaffold.Dispatcher.Dispatch(() => window.Page = scaffold);

        return result;
    }

    /// <summary>The platform view of the nav bar host showing <paramref name="title"/>.</summary>
    internal static object? CaptureNavBar(Element element, string title)
    {
        if (FindScaffold(element) is not { } scaffold)
        {
            return null;
        }

        // The host type is internal to the library: matched by name, and by the title it shows.
        var host = ((IVisualTreeElement) scaffold).GetVisualTreeDescendants()
                                                 .OfType<View>()
                                                 .FirstOrDefault(
                                                     candidate => candidate.GetType().Name == "ScaffoldNavBarHost"
                                                                  && ((IVisualTreeElement) candidate).GetVisualTreeDescendants()
                                                                                                      .OfType<Label>()
                                                                                                      .Any(label => label.Text == title)
                                                 );

        return host is null ? null : PlatformViewOf(host);
    }

    /// <summary>
    /// <c>superview=null</c>, <c>superview={type}</c>, or <c>threw={type}</c> when the superview is
    /// a native object whose managed peer is gone.
    /// </summary>
    internal static string DescribeSuperview(object? view, bool collect = false)
    {
#if IOS || MACCATALYST
        if (view is not UIKit.UIView platformView)
        {
            return "no-view";
        }

        if (collect)
        {
            CollectOnMainThread();
        }

        try
        {
            return platformView.Superview is { } superview ? $"superview={superview.GetType().Name}" : "superview=null";
        }
        catch (Exception exception)
        {
            return $"threw={exception.GetType().Name}";
        }
#else
        _ = view;
        _ = collect;

        return "n/a";
#endif
    }

    private static Scaffold? FindScaffold(Element element)
    {
        var ancestor = element.Parent;

        while (ancestor is not null and not Scaffold)
        {
            ancestor = ancestor.Parent;
        }

        return ancestor as Scaffold;
    }

    private static View? TabBarViewOf(Element element)
        => FindScaffold(element)?.Areas.OfType<ScaffoldTabBar>().FirstOrDefault()?.TabBarView;

    /// <summary>What a host mounts for a MAUI view: its container when it has one.</summary>
    private static object? PlatformViewOf(View view)
#if IOS || MACCATALYST
        => view.Handler is IPlatformViewHandler handler ? handler.ContainerView ?? handler.PlatformView : null;
#else
        => null;
#endif
}
