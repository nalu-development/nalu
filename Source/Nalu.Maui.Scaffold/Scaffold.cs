using System.Diagnostics.CodeAnalysis;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// Mobile-only (iOS/Android) application root replacing MAUI <see cref="Shell"/>,
/// driven by Nalu.Maui.Navigation.
/// </summary>
/// <remarks>
/// <para>
/// The scaffold owns the whole chrome (navigation bar, tab bar, flyouts) as Nalu-drawn virtual
/// views, hosts one navigation stack per <see cref="ScaffoldRoot"/>, and implements the
/// navigation host contracts so the standard Nalu fluent navigation API
/// (<c>Navigation.Relative()</c> / <c>Navigation.Absolute()</c>) drives it unchanged.
/// </para>
/// <para>
/// Structure: <see cref="Scaffold"/> → <see cref="ScaffoldArea"/> (or its
/// <see cref="ScaffoldTabBar"/> specialization) → <see cref="ScaffoldRoot"/>, each root hosting
/// an independent navigation stack. There are no developer-facing routes: absolute navigation is
/// type-based and resolves its destination from the registered root page types.
/// </para>
/// <code>
/// <![CDATA[
/// <nalu:Scaffold>
///     <nalu:ScaffoldTabBar>
///         <nalu:ScaffoldRoot Title="Home" Icon="home.png" PageType="pages:FeedPage" />
///         <nalu:ScaffoldRoot Title="Search" Icon="search.png" PageType="pages:SearchPage" />
///     </nalu:ScaffoldTabBar>
///     <!-- Terse form: implicitly wrapped into a single-root ScaffoldArea at parse time. -->
///     <nalu:ScaffoldRoot PageType="pages:SettingsPage" />
/// </nalu:Scaffold>
/// ]]>
/// </code>
/// </remarks>
[ContentProperty(nameof(Areas))]
public partial class Scaffold : ContentPage, IDisposable
{
    private bool _initialized;

    /// <summary>Occurs when a navigation lifecycle event is triggered.</summary>
    public event EventHandler<NavigationLifecycleEventArgs>? NavigationEvent;

    internal NavigationService? NavigationService { get; private set; }

    internal ScaffoldProxy? Proxy { get; private set; }

    /// <summary>The platform presenter realizing navigation; assigned by the Scaffold handler.</summary>
    internal IScaffoldPresenter? Presenter { get; set; }

    private static readonly BindablePropertyKey _currentAreaPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentArea), typeof(ScaffoldArea), typeof(Scaffold), null);

    /// <summary>Bindable property for <see cref="CurrentArea"/> (read-only).</summary>
    public static readonly BindableProperty CurrentAreaProperty = _currentAreaPropertyKey.BindableProperty;

    /// <summary>
    /// Attached property holding the start-edge (leading) flyout content.
    /// Resolution order, most specific wins: current <see cref="Page"/> →
    /// current <see cref="ScaffoldArea"/> → the <see cref="Scaffold"/> itself (global).
    /// No value at any level means the flyout is disabled (the default).
    /// The content is a plain MAUI view, logically parented to the element it is attached to
    /// (a per-page flyout inherits that page's BindingContext).
    /// </summary>
    public static readonly BindableProperty FlyoutStartProperty =
        BindableProperty.CreateAttached(nameof(FlyoutStart), typeof(View), typeof(Scaffold), null, propertyChanged: OnFlyoutContentChanged);

    /// <summary>
    /// Attached property holding the end-edge (trailing) flyout content.
    /// Same resolution rules as <see cref="FlyoutStartProperty"/>.
    /// </summary>
    public static readonly BindableProperty FlyoutEndProperty =
        BindableProperty.CreateAttached(nameof(FlyoutEnd), typeof(View), typeof(Scaffold), null, propertyChanged: OnFlyoutContentChanged);

    /// <summary>
    /// Attached property replacing the navigation bar title view for a <see cref="Page"/>.
    /// The page's <see cref="Page.Title"/> is used when no title view is set.
    /// </summary>
    public static readonly BindableProperty TitleViewProperty =
        BindableProperty.CreateAttached("TitleView", typeof(View), typeof(Scaffold), null);

    /// <summary>
    /// Attached property holding the navigation bar view. Resolution, most specific wins:
    /// current <see cref="Page"/> → current <see cref="ScaffoldArea"/> → the
    /// <see cref="Scaffold"/> itself, whose value defaults to a <see cref="ScaffoldNavBarView"/>
    /// (the default template carrying the whole styling surface) via the default value factory.
    /// The mounted view's binding context is the scaffold's <see cref="NavBarContext"/>.
    /// </summary>
    public static readonly BindableProperty NavBarViewProperty =
        BindableProperty.CreateAttached(
            "NavBarView",
            typeof(View),
            typeof(Scaffold),
            null,
            defaultValueCreator: bindable => bindable is Scaffold ? new ScaffoldNavBarView() : null
        );

    /// <summary>
    /// Attached property controlling navigation bar visibility for a <see cref="Page"/>.
    /// Defaults to true. Visibility changes animate and reach the page as a safe-area inset
    /// change, not a page relayout.
    /// </summary>
    public static readonly BindableProperty IsNavBarVisibleProperty =
        BindableProperty.CreateAttached("IsNavBarVisible", typeof(bool), typeof(Scaffold), true);

    /// <summary>
    /// Attached property controlling the nav bar's start-drawer button
    /// (<see cref="ScaffoldFlyoutButtonVisibility.Auto"/> default: shown at stack roots only).
    /// Resolution, most specific set value wins: current <see cref="Page"/> →
    /// current <see cref="ScaffoldArea"/> → the <see cref="Scaffold"/>.
    /// </summary>
    public static readonly BindableProperty FlyoutStartButtonVisibilityProperty =
        BindableProperty.CreateAttached("FlyoutStartButtonVisibility", typeof(ScaffoldFlyoutButtonVisibility), typeof(Scaffold), ScaffoldFlyoutButtonVisibility.Auto);

    /// <summary>
    /// Attached property controlling the nav bar's end-drawer button.
    /// Same semantics as <see cref="FlyoutStartButtonVisibilityProperty"/>.
    /// </summary>
    public static readonly BindableProperty FlyoutEndButtonVisibilityProperty =
        BindableProperty.CreateAttached("FlyoutEndButtonVisibility", typeof(ScaffoldFlyoutButtonVisibility), typeof(Scaffold), ScaffoldFlyoutButtonVisibility.Auto);

    /// <summary>
    /// Attached property controlling tab bar visibility for a <see cref="Page"/>:
    /// <see cref="ScaffoldTabBarVisibility.Visible"/> (default),
    /// <see cref="ScaffoldTabBarVisibility.Hidden"/>, or
    /// <see cref="ScaffoldTabBarVisibility.Auto"/> (hidden while the current stack has pushed
    /// pages). Visibility changes animate and reach the page as a safe-area inset change.
    /// </summary>
    public static readonly BindableProperty TabBarVisibilityProperty =
        BindableProperty.CreateAttached("TabBarVisibility", typeof(ScaffoldTabBarVisibility), typeof(Scaffold), ScaffoldTabBarVisibility.Visible);


    /// <summary>
    /// Gets or sets the page type of the root to open at startup. Optional: when not set, the
    /// first root of the first area is opened. Must match the <see cref="ScaffoldRoot.PageType"/>
    /// of one of the scaffold's roots.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? InitialRootPageType { get; set; }

    /// <summary>Gets the destination areas composing the application structure.</summary>
    public IList<ScaffoldArea> Areas { get; }

    /// <summary>
    /// Gets the selected area (defaults to the first one). Read-only: selection changes only
    /// through the Nalu navigation engine (absolute navigation or flyout interaction), so
    /// lifecycle events and leaving guards can never be bypassed. Observable via binding.
    /// </summary>
    public ScaffoldArea? CurrentArea
    {
        get => (ScaffoldArea?)GetValue(CurrentAreaProperty);
        internal set => SetValue(_currentAreaPropertyKey, value);
    }

    /// <summary>Gets or sets the global start-edge flyout content (see <see cref="FlyoutStartProperty"/>).</summary>
    public View? FlyoutStart
    {
        get => (View?)GetValue(FlyoutStartProperty);
        set => SetValue(FlyoutStartProperty, value);
    }

    /// <summary>Gets or sets the global end-edge flyout content (see <see cref="FlyoutEndProperty"/>).</summary>
    public View? FlyoutEnd
    {
        get => (View?)GetValue(FlyoutEndProperty);
        set => SetValue(FlyoutEndProperty, value);
    }

    /// <summary>Initializes a new <see cref="Scaffold"/>.</summary>
    public Scaffold()
    {
        Areas = new ScaffoldElementCollection<ScaffoldArea>(this);

        // Hosted pages chain their Page.Navigation to this proxy (they are logical children):
        // pops requested through the classic INavigation API route into the engine.
        ((Microsoft.Maui.Controls.Internals.INavigationProxy)this).NavigationProxy.Inner = new ScaffoldNavigationImpl(this);
    }

    /// <summary>Gets the start-edge flyout content attached to an element.</summary>
    public static View? GetFlyoutStart(BindableObject bindable) => (View?)bindable.GetValue(FlyoutStartProperty);

    /// <summary>Sets the start-edge flyout content attached to an element.</summary>
    public static void SetFlyoutStart(BindableObject bindable, View? value) => bindable.SetValue(FlyoutStartProperty, value);

    /// <summary>Gets the end-edge flyout content attached to an element.</summary>
    public static View? GetFlyoutEnd(BindableObject bindable) => (View?)bindable.GetValue(FlyoutEndProperty);

    /// <summary>Sets the end-edge flyout content attached to an element.</summary>
    public static void SetFlyoutEnd(BindableObject bindable, View? value) => bindable.SetValue(FlyoutEndProperty, value);

    private static void OnFlyoutContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // Flyout content participates in the element tree at its attachment point:
        // BindingContext/resource inheritance and tooling visibility come for free.
        if (bindable is Element element)
        {
            if (oldValue is View oldView)
            {
                element.RemoveLogicalChild(oldView);
            }

            if (newValue is View newView)
            {
                element.AddLogicalChild(newView);
            }
        }
    }

    /// <summary>
    /// Opens the flyout for the given side, resolving its content from the current page,
    /// then the current area, then the scaffold's own value. No-op when no content is
    /// configured at any level or the scaffold is not presented yet.
    /// </summary>
    /// <param name="side">The edge the flyout slides in from.</param>
    public Task OpenFlyoutAsync(ScaffoldFlyoutSide side)
        => Presenter is { } presenter && ResolveFlyoutContent(side) is { } content
            ? presenter.OpenFlyoutAsync(side, content)
            : Task.CompletedTask;

    /// <summary>Closes the open flyout (or any other presented overlay), if any.</summary>
    public Task CloseFlyoutAsync()
        => Presenter is { } presenter ? presenter.CloseOverlayAsync() : Task.CompletedTask;

    /// <summary>Dismisses the currently presented overlay (flyout or bottom panel), if any.</summary>
    public Task CloseOverlayAsync()
        => Presenter is { } presenter ? presenter.CloseOverlayAsync() : Task.CompletedTask;

    /// <summary>
    /// Presents a panel anchored above the bottom chrome — the primitive behind the default
    /// tab bar template's "More" overflow, available to custom tab bars too. A fullscreen scrim
    /// renders BELOW the tab bar in z-order: the bar stays undimmed and interactive (a bar tap
    /// dismisses the panel and performs its own action). Scrim tap, system back and any
    /// navigation dismiss the panel. Toggle semantics: when an overlay is already presented,
    /// the call dismisses it instead.
    /// </summary>
    /// <param name="content">
    /// The panel view; its horizontal <see cref="View.Margin"/> insets it from the container
    /// edges. The view is attached to this scaffold's element tree while presented (unless it
    /// already has a parent) and is reusable across presentations — handlers are not
    /// disconnected on close.
    /// </param>
    /// <param name="scrimColor">The scrim color; a theme-aware translucent black when omitted.</param>
    public Task OpenTabBarPanelAsync(View content, Color? scrimColor = null)
    {
        if (Presenter is not { } presenter)
        {
            return Task.CompletedTask;
        }

        if (presenter.HasOverlay)
        {
            return presenter.CloseOverlayAsync();
        }

        var attach = content.Parent is null;

        if (attach)
        {
            AddLogicalChild(content);
        }

        var dark = (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;

        return presenter.OpenTabBarPanelAsync(
            content,
            scrimColor ?? Colors.Black.WithAlpha(dark ? 0.55f : 0.45f),
            disconnectOnClose: false,
            cleanup: attach ? () => RemoveLogicalChild(content) : null
        );
    }

    /// <summary>
    /// Selects the given root through the navigation engine: switching to another root restores
    /// its preserved navigation stack; re-selecting the current root pops its stack back to the
    /// root page. Guards and lifecycle events always run. No-op when the root is already current
    /// with an empty stack.
    /// </summary>
    internal async Task<bool> SelectRootAsync(ScaffoldRoot root)
    {
        if (NavigationService is not { } navigationService
            || Proxy?.BuildRootSelectionNavigation(root) is not { } navigation)
        {
            return false;
        }

        return await navigationService.GoToAsync(navigation).ConfigureAwait(true);
    }

    internal View? ResolveFlyoutContent(ScaffoldFlyoutSide side)
    {
        var property = side == ScaffoldFlyoutSide.Start ? FlyoutStartProperty : FlyoutEndProperty;
        var currentRoot = (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root;
        var stack = currentRoot?.NavigationStack;
        var currentPage = stack is null ? null
            : stack.PushedPages.Count > 0 ? stack.PushedPages[^1].Page
            : stack.RootPage;

        return (currentPage?.GetValue(property) as View)
               ?? (CurrentArea?.GetValue(property) as View)
               ?? GetValue(property) as View;
    }

    /// <summary>Gets the navigation bar title view attached to a page.</summary>
    public static View? GetTitleView(BindableObject bindable) => (View?)bindable.GetValue(TitleViewProperty);

    /// <summary>Sets the navigation bar title view attached to a page.</summary>
    public static void SetTitleView(BindableObject bindable, View? value) => bindable.SetValue(TitleViewProperty, value);

    /// <summary>Gets the navigation bar view attached to an element.</summary>
    public static View? GetNavBarView(BindableObject bindable) => (View?)bindable.GetValue(NavBarViewProperty);

    /// <summary>Sets the navigation bar view attached to an element.</summary>
    public static void SetNavBarView(BindableObject bindable, View? value) => bindable.SetValue(NavBarViewProperty, value);

    /// <summary>Gets whether the navigation bar is visible for a page.</summary>
    public static bool GetIsNavBarVisible(BindableObject bindable) => (bool)bindable.GetValue(IsNavBarVisibleProperty);

    /// <summary>Sets whether the navigation bar is visible for a page.</summary>
    public static void SetIsNavBarVisible(BindableObject bindable, bool value) => bindable.SetValue(IsNavBarVisibleProperty, value);

    /// <summary>Gets the start-drawer button policy attached to an element.</summary>
    public static ScaffoldFlyoutButtonVisibility GetFlyoutStartButtonVisibility(BindableObject bindable) => (ScaffoldFlyoutButtonVisibility)bindable.GetValue(FlyoutStartButtonVisibilityProperty);

    /// <summary>Sets the start-drawer button policy attached to an element.</summary>
    public static void SetFlyoutStartButtonVisibility(BindableObject bindable, ScaffoldFlyoutButtonVisibility value) => bindable.SetValue(FlyoutStartButtonVisibilityProperty, value);

    /// <summary>Gets the end-drawer button policy attached to an element.</summary>
    public static ScaffoldFlyoutButtonVisibility GetFlyoutEndButtonVisibility(BindableObject bindable) => (ScaffoldFlyoutButtonVisibility)bindable.GetValue(FlyoutEndButtonVisibilityProperty);

    /// <summary>Sets the end-drawer button policy attached to an element.</summary>
    public static void SetFlyoutEndButtonVisibility(BindableObject bindable, ScaffoldFlyoutButtonVisibility value) => bindable.SetValue(FlyoutEndButtonVisibilityProperty, value);

    /// <summary>
    /// Gets the observable state the mounted nav bar view binds to (title, back/drawer button
    /// availability, commands) — the binding context of the default template and of custom
    /// nav bar views alike.
    /// </summary>
    public ScaffoldNavBarContext NavBarContext => field ??= new ScaffoldNavBarContext(this);

    /// <summary>
    /// Resolves the nav bar view for the given page: page attachment → current area attachment
    /// → the scaffold's own value (defaulting to the built-in <see cref="ScaffoldNavBarView"/>).
    /// The resolved view is attached to this scaffold's element tree on mount.
    /// </summary>
    internal View? ResolveNavBarView(Page currentPage)
        => GetNavBarView(currentPage)
           ?? (CurrentArea is { } area ? GetNavBarView(area) : null)
           ?? GetNavBarView(this);

    /// <summary>Gets the tab bar visibility policy attached to a page.</summary>
    public static ScaffoldTabBarVisibility GetTabBarVisibility(BindableObject bindable) => (ScaffoldTabBarVisibility)bindable.GetValue(TabBarVisibilityProperty);

    /// <summary>Sets the tab bar visibility policy attached to a page.</summary>
    public static void SetTabBarVisibility(BindableObject bindable, ScaffoldTabBarVisibility value) => bindable.SetValue(TabBarVisibilityProperty, value);

    /// <summary>
    /// Resolves the effective tab bar visibility for the given page hosted by the given root:
    /// the page's <see cref="TabBarVisibilityProperty"/> policy, with
    /// <see cref="ScaffoldTabBarVisibility.Auto"/> meaning "visible only at the stack root".
    /// </summary>
    internal static bool ComputeTabBarVisible(ScaffoldRoot root, Page page)
        => GetTabBarVisibility(page) switch
        {
            ScaffoldTabBarVisibility.Hidden => false,
            ScaffoldTabBarVisibility.Auto => root.NavigationStack.PushedPages.Count == 0,
            _ => true
        };


    // System back (Android hardware/gesture back) is handled by the OnBackPressedDispatcher
    // callback registered in the Android partial: it routes through the Nalu navigation engine
    // (guards and lifecycle always run) and leaves root pages to the platform default — the app
    // backgrounds with the native predictive back-to-home preview intact.
    // Page.OnBackButtonPressed on hosted pages is deliberately NOT supported: it only fires for
    // hardware back, so confirmation logic written there is silently bypassed by on-screen pops.
    // ILeavingGuard is the one confirmation mechanism, covering every leave path uniformly.

    /// <summary>
    /// Routes back requests arriving through MAUI's legacy/synthetic channel (e.g. automation
    /// drivers, platforms without dispatcher-based back) into the navigation engine, matching
    /// the behavior of the Android dispatcher callback. Guards and lifecycle always run.
    /// </summary>
    /// <returns>True when a pop was dispatched.</returns>
    protected override bool OnBackButtonPressed()
    {
        // Overlays (flyout, tab bar overflow panel) dismiss before the navigation engine
        // is ever consulted — the same policy §7.2 defines for popups.
        if (Presenter is { HasOverlay: true } presenter)
        {
            Dispatcher.Dispatch(() => presenter.CloseOverlayAsync().FireAndForget(Handler));

            return true;
        }

        if (NavigationService is { } navigationService
            && Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy rootProxy
            && rootProxy.Root.NavigationStack.PushedPages.Count > 0)
        {
            Dispatcher.Dispatch(() => navigationService.GoToAsync(Nalu.Navigation.Relative().Pop()).FireAndForget(Handler));

            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Bootstraps the navigation engine and synchronizes the presenter with the startup
    /// destination. Invoked by <see cref="ScaffoldHandler"/> on every handler connection.
    /// </summary>
    internal async Task InitializeAndPresentAsync(IServiceProvider services)
    {
        await InitializeAsync(services);

        // Initial display: synchronize the presenter with the startup destination.
        if (Presenter is { } presenter && Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy currentRoot)
        {
            await presenter.SynchronizeAsync(currentRoot.Root, ScaffoldPresentationHint.None);
        }
    }

    /// <summary>
    /// Builds the navigation host proxy and initializes the Nalu navigation engine on the
    /// startup destination. Idempotent; invoked automatically when the handler attaches.
    /// </summary>
    internal Task InitializeAsync(IServiceProvider services)
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;

        var navigationService = (NavigationService)services.GetRequiredService<INavigationService>();
        NavigationService = navigationService;

        var proxy = new ScaffoldProxy(this, navigationService);
        Proxy = proxy;

        var initialSegmentName = proxy.ResolveInitialSegmentName(InitialRootPageType, navigationService.Configuration);
        proxy.InitializeWithContent(initialSegmentName);

        return navigationService.InitializeAsync(proxy, initialSegmentName, null);
    }

    internal void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args)
        => NavigationEvent?.Invoke(this, args);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the scaffold's live pages, DI scopes and page models.</summary>
    /// <param name="disposing">True when disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Proxy is { } proxy)
        {
            proxy.Dispose();
            NavigationService?.OnShellProxyDisposed(proxy);
            Proxy = null;
        }
    }
}
