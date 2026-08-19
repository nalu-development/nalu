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
public partial class Scaffold : Page, IPageContainer<Page>, IDisposable
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
    /// Resolution walks the current stack as an override chain, most specific first: the
    /// topmost pushed page that SET the value, older pushed pages, the root page, the current
    /// <see cref="ScaffoldArea"/>, then the <see cref="Scaffold"/> itself — so a page's drawer
    /// survives pushes that don't override it. Content alone does not enable a drawer: the
    /// matching <see cref="ScaffoldFlyoutMode"/> must also be non-<c>Disabled</c>.
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
    /// Attached property controlling the start-edge drawer's behavior
    /// (<see cref="ScaffoldFlyoutMode"/>). Resolution, most specific SET value wins: the
    /// topmost pushed page that SET it, older pushed pages, the root page, the current
    /// <see cref="ScaffoldArea"/>, then the <see cref="Scaffold"/>. Defaults to
    /// <see cref="ScaffoldFlyoutMode.Disabled"/> — a drawer requires content AND an
    /// explicitly enabling mode.
    /// </summary>
    public static readonly BindableProperty FlyoutStartModeProperty =
        BindableProperty.CreateAttached("FlyoutStartMode", typeof(ScaffoldFlyoutMode), typeof(Scaffold), ScaffoldFlyoutMode.Disabled);

    /// <summary>
    /// Attached property controlling the end-edge drawer's behavior.
    /// Same semantics as <see cref="FlyoutStartModeProperty"/>.
    /// </summary>
    public static readonly BindableProperty FlyoutEndModeProperty =
        BindableProperty.CreateAttached("FlyoutEndMode", typeof(ScaffoldFlyoutMode), typeof(Scaffold), ScaffoldFlyoutMode.Disabled);

    /// <summary>Bindable property for <see cref="FlyoutStartOptions"/>.</summary>
    public static readonly BindableProperty FlyoutStartOptionsProperty =
        BindableProperty.Create(nameof(FlyoutStartOptions), typeof(ScaffoldFlyoutOptions), typeof(Scaffold), null);

    /// <summary>Bindable property for <see cref="FlyoutEndOptions"/>.</summary>
    public static readonly BindableProperty FlyoutEndOptionsProperty =
        BindableProperty.Create(nameof(FlyoutEndOptions), typeof(ScaffoldFlyoutOptions), typeof(Scaffold), null);

    private static readonly BindablePropertyKey _isFlyoutStartOpenPropertyKey =
        BindableProperty.CreateReadOnly(nameof(IsFlyoutStartOpen), typeof(bool), typeof(Scaffold), false);

    /// <summary>Bindable property for <see cref="IsFlyoutStartOpen"/> (read-only).</summary>
    public static readonly BindableProperty IsFlyoutStartOpenProperty = _isFlyoutStartOpenPropertyKey.BindableProperty;

    private static readonly BindablePropertyKey _isFlyoutEndOpenPropertyKey =
        BindableProperty.CreateReadOnly(nameof(IsFlyoutEndOpen), typeof(bool), typeof(Scaffold), false);

    /// <summary>Bindable property for <see cref="IsFlyoutEndOpen"/> (read-only).</summary>
    public static readonly BindableProperty IsFlyoutEndOpenProperty = _isFlyoutEndOpenPropertyKey.BindableProperty;

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
    /// Attached property holding the nav bar strip presentation
    /// (<see cref="ScaffoldNavBarAppearance"/>). Each appearance property resolves
    /// INDEPENDENTLY, most specific set value wins: current <see cref="Page"/> → current
    /// <see cref="ScaffoldArea"/> → the <see cref="Scaffold"/> → built-in defaults — a
    /// page-level appearance is a delta over the global one. The attached object inherits the
    /// binding context of its element, so its properties can be bound (and animated) from
    /// page state.
    /// </summary>
    public static readonly BindableProperty NavBarAppearanceProperty =
        BindableProperty.CreateAttached(
            "NavBarAppearance",
            typeof(ScaffoldNavBarAppearance),
            typeof(Scaffold),
            null,
            propertyChanged: OnNavBarAppearanceChanged
        );

    /// <summary>
    /// Attached property controlling navigation bar visibility for a <see cref="Page"/>.
    /// Defaults to true. Visibility changes animate and reach the page as a safe-area inset
    /// change, not a page relayout.
    /// </summary>
    public static readonly BindableProperty IsNavBarVisibleProperty =
        BindableProperty.CreateAttached("IsNavBarVisible", typeof(bool), typeof(Scaffold), true);

    /// <summary>
    /// Attached property (per <see cref="Page"/>) pointing at the page's primary scrollable
    /// view: its NATIVE scroll position feeds <see cref="ScaffoldNavBarContext.ScrollOffset"/>
    /// and <see cref="ScaffoldNavBarContext.IsScrolledUnder"/> per frame — the channel behind
    /// scroll-driven chrome (large-title collapses, bar fade-ins). The referenced view need
    /// not BE the scrollable: its platform subtree is searched a few levels deep for the
    /// actual scrollable platform view (component roots often wrap it — e.g. VirtualScroll).
    /// </summary>
    public static readonly BindableProperty ScrollTrackerProperty =
        BindableProperty.CreateAttached("ScrollTracker", typeof(View), typeof(Scaffold), null);

    /// <summary>
    /// Attached property providing the default <c>RampStart</c> of every
    /// <see cref="ScrollValueExtension"/>/<see cref="ThemeScrollValueExtension"/> usage on the
    /// page (resolution: page → current area → scaffold → 0): declare the interpolation ramp
    /// ONCE, use it everywhere on the page.
    /// </summary>
    public static readonly BindableProperty ScrollRampStartProperty =
        BindableProperty.CreateAttached("ScrollRampStart", typeof(double), typeof(Scaffold), 0.0);

    /// <summary>
    /// Attached property providing the default <c>RampEnd</c> of every
    /// <see cref="ScrollValueExtension"/>/<see cref="ThemeScrollValueExtension"/> usage on the
    /// page (resolution: page → current area → scaffold → 100).
    /// </summary>
    public static readonly BindableProperty ScrollRampEndProperty =
        BindableProperty.CreateAttached("ScrollRampEnd", typeof(double), typeof(Scaffold), 100.0);

    /// <summary>
    /// Attached property (per <see cref="Page"/>, default false) laying the page out UNDER the
    /// nav bar: the bar's footprint is not applied as a top inset — content starts at the very
    /// top edge (the page's own <c>SafeAreaEdges</c> decides how it treats the raw system
    /// insets) and the bar draws over it. Pair with a page-level
    /// <see cref="NavBarAppearanceProperty"/> (e.g. a transparent <see
    /// cref="ScaffoldNavBarAppearance.Background"/>) for full-bleed headers whose bar
    /// materializes on scroll.
    /// </summary>
    public static readonly BindableProperty NavBarOverlapsContentProperty =
        BindableProperty.CreateAttached("NavBarOverlapsContent", typeof(bool), typeof(Scaffold), false);

    /// <summary>
    /// Attached property declaring how a surface reacts to the soft keyboard
    /// (<see cref="ScaffoldKeyboardMode"/>). On a <see cref="Page"/> it is the page's policy —
    /// unset pages inherit the value declared on the <see cref="Scaffold"/> itself, and the
    /// scaffold's default is <see cref="ScaffoldKeyboardMode.Resize"/> (the keyboard is a bottom
    /// inset for every page out of the box; declare <see cref="ScaffoldKeyboardMode.None"/> to opt a
    /// page out, or globally on the scaffold). On the content of a bottom sheet or a popup it is
    /// that overlay's policy (call-site options win over it), Resize when unset.
    /// </summary>
    public static readonly BindableProperty KeyboardModeProperty =
        BindableProperty.CreateAttached("KeyboardMode", typeof(ScaffoldKeyboardMode?), typeof(Scaffold), null);

    /// <summary>
    /// Attached property declaring the system status bar (and Android navigation bar) ICON
    /// style over a page's content (default <see cref="ScaffoldSystemBarStyle.Auto"/>).
    /// Resolution, most specific set value wins: current <see cref="Page"/> →
    /// current <see cref="ScaffoldArea"/> → the <see cref="Scaffold"/> — but an OPAQUE chrome
    /// surface covering the status-bar region (a materialized nav bar, an open flyout) always
    /// wins by its own brightness: the declaration describes the page's content, not the
    /// chrome above it. Declare it on full-bleed pages whose top content brightness the
    /// scaffold cannot know (photos, custom drawings).
    /// </summary>
    public static readonly BindableProperty SystemBarStyleProperty =
        BindableProperty.CreateAttached("SystemBarStyle", typeof(ScaffoldSystemBarStyle), typeof(Scaffold), ScaffoldSystemBarStyle.Auto);

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
    /// Attached property marking a view as a SHARED ELEMENT (the <c>android:transitionName</c>
    /// analogue): when the outgoing and incoming pages of a push/pop both contain a view with
    /// the same name, the element animates between its two geometries during the transition
    /// (images morph their aspect crop natively; other views transform-match with a cross-fade).
    /// </summary>
    public static readonly BindableProperty TransitionNameProperty =
        BindableProperty.CreateAttached("TransitionName", typeof(string), typeof(Scaffold), null);

    /// <summary>
    /// Attached property declaring the push/pop transition of a <see cref="Page"/>.
    /// Set on a page it overrides the scaffold-level value; set on the <see cref="Scaffold"/>
    /// itself it is the default for every page. Resolution: page-attached value →
    /// <see cref="ScaffoldPageTransition.SlideFromBottom"/> for modal pages → scaffold-level
    /// value → <see cref="ScaffoldPageTransition.Default"/>.
    /// The spec belongs to the PUSHED page: it enters with it and leaves with it reversed
    /// (pop and the iOS interactive edge swipe replay it backwards).
    /// </summary>
    public static readonly BindableProperty PageTransitionProperty =
        BindableProperty.CreateAttached("PageTransition", typeof(ScaffoldPageTransition), typeof(Scaffold), null);

    /// <summary>
    /// Attached property declaring a page's presentation mode:
    /// <see cref="ScaffoldPageMode.Default"/>, <see cref="ScaffoldPageMode.Modal"/> or
    /// <see cref="ScaffoldPageMode.DismissableModal"/>. Modal pages enter from the bottom,
    /// cover the tab bar and show a title-only nav bar. Plain <see cref="ScaffoldPageMode.Modal"/>
    /// blocks system back entirely (dismissal is programmatic); DismissableModal adds the close
    /// button and lets the Android system back pop through the engine.
    /// </summary>
    public static readonly BindableProperty PageModeProperty =
        BindableProperty.CreateAttached("PageMode", typeof(ScaffoldPageMode), typeof(Scaffold), ScaffoldPageMode.Default);

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
    /// first root of the first area is opened. Accepts the page type or its registered
    /// page-model type; must match the <see cref="ScaffoldRoot.PageType"/> of one of the
    /// scaffold's roots, or startup throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? InitialRootPageType { get; set; }

    /// <summary>
    /// Gets or sets the intent delivered to the startup root page (see
    /// <see cref="InitialRootPageType"/>): its model receives it through the standard
    /// <c>IEnteringAware&lt;TIntent&gt;</c> / <c>IAppearingAware&lt;TIntent&gt;</c> pipeline,
    /// exactly as if the root had been navigated to with that intent. Optional.
    /// </summary>
    public object? InitialIntent { get; set; }

    private static readonly BindablePropertyKey _currentPagePropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentPage), typeof(Page), typeof(Scaffold), null);

    /// <summary>Bindable property for <see cref="CurrentPage"/>.</summary>
    public static readonly BindableProperty CurrentPageProperty = _currentPagePropertyKey.BindableProperty;

    /// <summary>
    /// Gets the page currently presented by the scaffold: the top of the current root's
    /// navigation stack. Read-only and observable via binding; null only before the scaffold
    /// has initialized. Also exposed through <see cref="IPageContainer{T}"/>.
    /// </summary>
    public Page? CurrentPage => (Page?)GetValue(CurrentPageProperty);

    // The non-nullable interface contract is honored as soon as a page is presented; before the
    // first page is realized this yields null (like an empty NavigationPage's CurrentPage would)
    // — the instant an analytics SDK sampling at startup reports as "unknown screen".
    Page IPageContainer<Page>.CurrentPage => CurrentPage!;

    /// <summary>
    /// Recomputes <see cref="CurrentPage"/> from the proxy state — invoked by the proxy on
    /// selection changes and by the navigation stacks on push/pop/root mutations.
    /// </summary>
    internal void UpdateCurrentPage()
    {
        var stack = (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack;
        var page = stack is null
            ? null
            : stack.PushedPages.Count > 0
                ? stack.PushedPages[^1].Page
                : stack.RootPage;

        SetValue(_currentPagePropertyKey, page);
    }

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

    /// <summary>Gets or sets the start-edge drawer styling; null uses the built-in defaults.</summary>
    public ScaffoldFlyoutOptions? FlyoutStartOptions
    {
        get => (ScaffoldFlyoutOptions?)GetValue(FlyoutStartOptionsProperty);
        set => SetValue(FlyoutStartOptionsProperty, value);
    }

    /// <summary>Gets or sets the end-edge drawer styling; null uses the built-in defaults.</summary>
    public ScaffoldFlyoutOptions? FlyoutEndOptions
    {
        get => (ScaffoldFlyoutOptions?)GetValue(FlyoutEndOptionsProperty);
        set => SetValue(FlyoutEndOptionsProperty, value);
    }

    /// <summary>Gets whether the start-edge drawer is currently presented.</summary>
    public bool IsFlyoutStartOpen => (bool)GetValue(IsFlyoutStartOpenProperty);

    /// <summary>Gets whether the end-edge drawer is currently presented.</summary>
    public bool IsFlyoutEndOpen => (bool)GetValue(IsFlyoutEndOpenProperty);

    /// <summary>Occurs when the start-edge drawer finished opening.</summary>
    public event EventHandler? FlyoutStartOpened;

    /// <summary>Occurs when the start-edge drawer finished closing.</summary>
    public event EventHandler? FlyoutStartClosed;

    /// <summary>Occurs when the end-edge drawer finished opening.</summary>
    public event EventHandler? FlyoutEndOpened;

    /// <summary>Occurs when the end-edge drawer finished closing.</summary>
    public event EventHandler? FlyoutEndClosed;

    /// <summary>Initializes a new <see cref="Scaffold"/>.</summary>
    public Scaffold()
    {
        Areas = new ScaffoldElementCollection<ScaffoldArea>(this);
        ((System.Collections.Specialized.INotifyCollectionChanged)Areas).CollectionChanged += OnAreasCollectionChanged;

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

    /// <summary>Gets the start-edge drawer mode attached to an element.</summary>
    public static ScaffoldFlyoutMode GetFlyoutStartMode(BindableObject bindable) => (ScaffoldFlyoutMode)bindable.GetValue(FlyoutStartModeProperty);

    /// <summary>Sets the start-edge drawer mode attached to an element.</summary>
    public static void SetFlyoutStartMode(BindableObject bindable, ScaffoldFlyoutMode value) => bindable.SetValue(FlyoutStartModeProperty, value);

    /// <summary>Gets the end-edge drawer mode attached to an element.</summary>
    public static ScaffoldFlyoutMode GetFlyoutEndMode(BindableObject bindable) => (ScaffoldFlyoutMode)bindable.GetValue(FlyoutEndModeProperty);

    /// <summary>Sets the end-edge drawer mode attached to an element.</summary>
    public static void SetFlyoutEndMode(BindableObject bindable, ScaffoldFlyoutMode value) => bindable.SetValue(FlyoutEndModeProperty, value);

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

    private ScaffoldOverlayRequest? _flyoutRequest;
    private ScaffoldOverlayRequest? _tabBarPanelRequest;

    /// <summary>Gets whether a tab bar panel is currently presented.</summary>
    public bool HasTabBarPanel => _tabBarPanelRequest is not null;

    /// <summary>The default overlay scrim: a theme-aware translucent black.</summary>
    internal static Brush CreateDefaultScrim()
    {
        var dark = (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;

        return new SolidColorBrush(Colors.Black.WithAlpha(dark ? 0.55f : 0.45f));
    }

    /// <summary>
    /// Opens the flyout for the given side, resolving its content through the stack override
    /// chain (see <see cref="FlyoutStartProperty"/>). No-op when the drawer does not
    /// exist — no content configured at any level, its mode resolves to
    /// <see cref="ScaffoldFlyoutMode.Disabled"/> (or <see cref="ScaffoldFlyoutMode.Auto"/>
    /// while pages are pushed) — or the scaffold is not presented yet, or a flyout is
    /// already open.
    /// </summary>
    /// <param name="side">The edge the flyout slides in from.</param>
    public async Task OpenFlyoutAsync(ScaffoldFlyoutSide side)
    {
        if (Presenter is not { } presenter
            || _flyoutRequest is not null
            || !ComputeFlyoutAvailable(side)
            || ResolveFlyoutContent(side) is not { } content)
        {
            return;
        }

        var request = new ScaffoldOverlayRequest
        {
            Kind = ScaffoldOverlayKind.Flyout,
            Content = content,
            FlyoutSide = side,
            Scrim = GetEffectiveFlyoutOptions(side).ComputeScrim(),
            ScrimAutomationId = "ScaffoldFlyoutScrim"
        };

        request.Cleanup = () =>
        {
            if (ReferenceEquals(_flyoutRequest, request))
            {
                _flyoutRequest = null;
            }

            OnFlyoutDismissed(side);
        };

        _flyoutRequest = request;

        // The guard beats a close racing the enter animation: cleanup runs (and clears the
        // request) BEFORE presentation settles, and the late presented-event must not overwrite
        // the dismissed state.
        if (await presenter.ShowOverlayAsync(request)
            && ReferenceEquals(_flyoutRequest, request))
        {
            OnFlyoutPresented(side);
        }
    }

    /// <summary>Closes the open flyout, if any (other overlays are unaffected).</summary>
    public Task CloseFlyoutAsync()
        => Presenter is { } presenter && _flyoutRequest is { } request
            ? presenter.CloseOverlayAsync(request)
            : Task.CompletedTask;

    /// <summary>
    /// Presents a panel anchored above the bottom chrome — the primitive behind the default
    /// tab bar template's "More" overflow, available to custom tab bars too. A fullscreen scrim
    /// renders BELOW the tab bar in z-order: the bar stays undimmed and interactive (a bar tap
    /// dismisses the panel and performs its own action). Scrim tap, system back and any
    /// navigation dismiss the panel.
    /// </summary>
    /// <param name="content">
    /// The panel view; its horizontal <see cref="View.Margin"/> insets it from the container
    /// edges. The view is attached to this scaffold's element tree while presented (unless it
    /// already has a parent) and is reusable across presentations — handlers are not
    /// disconnected on close.
    /// </param>
    /// <param name="scrim">The scrim brush; a theme-aware translucent black when omitted.</param>
    /// <param name="closeIfOpened">
    /// When true (the default) and a panel is already presented, the call dismisses it (toggle).
    /// When false, the presented panel's content is REPLACED in place — content crossfades, the
    /// scrim brush updates, no scrim re-animation.
    /// </param>
    public Task ShowTabBarPanelAsync(View content, Brush? scrim = null, bool closeIfOpened = true)
        => ShowTabBarPanelCoreAsync(content, scrim, closeIfOpened, disconnectContentOnClose: false, cleanup: null);

    /// <summary>
    /// Presents a popup in the top overlay layer — above all chrome and previously presented
    /// overlays (popups stack in open order, each above its own scrim). Placement, scrim and
    /// dismissal policy come from <paramref name="options"/>; the tab bar's safe-area footprint
    /// never affects the presentation area (only system insets do).
    /// </summary>
    /// <param name="content">
    /// The popup view, measured within the safe presentation area. Attached to this scaffold's
    /// element tree while presented (styles and BindingContext flow); treated as single-use —
    /// its handlers are disconnected when the popup closes.
    /// </param>
    /// <param name="options">The presentation options; sensible popup defaults when omitted.</param>
    /// <returns>
    /// The lifetime handle: close it, await <see cref="IScaffoldPopup.Closed"/>, or scope it
    /// with <c>await using</c>. When the scaffold is not presented yet, the returned handle is
    /// already closed (<see cref="IScaffoldPopup.IsOpen"/> is false).
    /// </returns>
    public async Task<IScaffoldPopup> ShowPopupAsync(View content, ScaffoldPopupOptions? options = null)
    {
        var handle = new ScaffoldPopupHandle();

        if (Presenter is not { } presenter)
        {
            handle.MarkClosed();

            return handle;
        }

        var attach = content.Parent is null;

        if (attach)
        {
            AddLogicalChild(content);
        }

        // Per-property resolution: call-site option ?? the content's attached value ?? default.
        var request = new ScaffoldOverlayRequest
        {
            Kind = ScaffoldOverlayKind.Popup,
            Content = content,
            Scrim = options?.Scrim ?? ScaffoldPopup.GetScrim(content) ?? CreateDefaultScrim(),
            CloseOnScrimTap = options?.CloseOnScrimTap ?? ScaffoldPopup.GetCloseOnScrimTap(content) ?? true,
            CloseOnBack = options?.CloseOnBack ?? ScaffoldPopup.GetCloseOnBack(content) ?? true,
            KeyboardMode = options?.KeyboardMode ?? GetKeyboardMode(content) ?? ScaffoldKeyboardMode.Resize,
            DisconnectContentOnClose = true,
            PopupPresentation = new ScaffoldPopupPresentation(
                options?.Placement ?? ScaffoldPopup.GetPlacement(content) ?? ScaffoldPopupPlacement.Center,
                options?.Anchor,
                options?.AnchorOffset ?? Point.Zero,
                options?.Margin ?? ScaffoldPopup.GetMargin(content) ?? new Thickness(16),
                options?.CustomPlacer
            ),
            ScrimAutomationId = "PopupScrim"
        };

        request.Cleanup = () =>
        {
            if (attach)
            {
                RemoveLogicalChild(content);
            }

            handle.MarkClosed();
        };

        handle.Attach(this, request);

        // On failure the presenter has already run Cleanup — the handle comes back closed.
        await presenter.ShowOverlayAsync(request);

        return handle;
    }

    /// <summary>
    /// Presents a bottom sheet in the top overlay layer: it slides from the bottom edge over
    /// any chrome (only system insets shape the presentation area — the bottom inset pads the
    /// content clear of the home indicator). The content is wrapped in a
    /// <see cref="ScaffoldBottomSheetView"/> handling drag between detents and
    /// pull-down-to-close entirely at the virtual view layer.
    /// </summary>
    /// <param name="content">
    /// The sheet content. Attached to this scaffold's element tree while presented (styles and
    /// BindingContext flow); treated as single-use — handlers are disconnected on close.
    /// </param>
    /// <param name="options">The presentation options; a content-hugging, pull-down-closable sheet when omitted.</param>
    /// <returns>
    /// The lifetime handle: close it, await <see cref="IScaffoldPopup.Closed"/>, or scope it
    /// with <c>await using</c>. When the scaffold is not presented yet, the returned handle is
    /// already closed (<see cref="IScaffoldPopup.IsOpen"/> is false).
    /// </returns>
    public async Task<IScaffoldPopup> ShowBottomSheetAsync(View content, ScaffoldBottomSheetOptions? options = null)
    {
        var handle = new ScaffoldPopupHandle();

        if (Presenter is not { } presenter)
        {
            handle.MarkClosed();

            return handle;
        }

        // Per-property resolution: call-site option ?? the content's attached value ?? default.
        var presentation = new ScaffoldSheetPresentation(
            options?.Detents ?? ScaffoldBottomSheet.GetDetents(content) ?? [ScaffoldSheetDetent.Content],
            options?.InitialDetent ?? ScaffoldBottomSheet.GetInitialDetent(content) ?? 0,
            options?.AllowPullDownToClose ?? ScaffoldBottomSheet.GetAllowPullDownToClose(content) ?? true,
            options?.ShowDragHandle ?? ScaffoldBottomSheet.GetShowDragHandle(content) ?? true,
            options?.MaxWidth ?? ScaffoldBottomSheet.GetMaxWidth(content) ?? double.PositiveInfinity
        );

        var sheetView = new ScaffoldBottomSheetView(content, presentation);
        AddLogicalChild(sheetView);

        var request = new ScaffoldOverlayRequest
        {
            Kind = ScaffoldOverlayKind.BottomSheet,
            Content = sheetView,
            Scrim = options?.Scrim ?? ScaffoldBottomSheet.GetScrim(content) ?? CreateDefaultScrim(),
            CloseOnScrimTap = options?.CloseOnScrimTap ?? ScaffoldBottomSheet.GetCloseOnScrimTap(content) ?? true,
            CloseOnBack = options?.CloseOnBack ?? ScaffoldBottomSheet.GetCloseOnBack(content) ?? true,
            KeyboardMode = options?.KeyboardMode ?? GetKeyboardMode(content) ?? ScaffoldKeyboardMode.Resize,
            DisconnectContentOnClose = true,
            ScrimAutomationId = "SheetScrim"
        };

        request.Cleanup = () =>
        {
            RemoveLogicalChild(sheetView);
            handle.MarkClosed();
        };

        handle.Attach(this, request);

        // Pull-down rides the same close path as every other dismissal (scrim fade + cleanup).
        sheetView.SetDismissCallback(() => presenter.CloseOverlayAsync(request));

        // On failure the presenter has already run Cleanup — the handle comes back closed.
        await presenter.ShowOverlayAsync(request);

        return handle;
    }

    /// <summary>Closes the presented tab bar panel, if any.</summary>
    public Task CloseTabBarPanelAsync()
        => Presenter is { } presenter && _tabBarPanelRequest is { } request
            ? presenter.CloseOverlayAsync(request)
            : Task.CompletedTask;

    /// <summary>The tab bar panel machinery shared by the public API and the default template's overflow.</summary>
    internal async Task ShowTabBarPanelCoreAsync(View content, Brush? scrim, bool closeIfOpened, bool disconnectContentOnClose, Action? cleanup)
    {
        if (Presenter is not { } presenter)
        {
            cleanup?.Invoke();

            return;
        }

        if (_tabBarPanelRequest is { } current && closeIfOpened)
        {
            // Toggle: the caller's fresh resources are released untouched.
            cleanup?.Invoke();
            await presenter.CloseOverlayAsync(current);

            return;
        }

        var attach = content.Parent is null;

        if (attach)
        {
            AddLogicalChild(content);
        }

        var request = new ScaffoldOverlayRequest
        {
            Kind = ScaffoldOverlayKind.TabBarPanel,
            Content = content,
            Scrim = scrim ?? CreateDefaultScrim(),
            DisconnectContentOnClose = disconnectContentOnClose,
            ScrimAutomationId = "TabBarPanelScrim"
        };

        request.Cleanup = () =>
        {
            if (ReferenceEquals(_tabBarPanelRequest, request))
            {
                _tabBarPanelRequest = null;
            }

            if (attach)
            {
                RemoveLogicalChild(content);
            }

            cleanup?.Invoke();
        };

        var replace = _tabBarPanelRequest is not null;
        _tabBarPanelRequest = request;

        if (replace)
        {
            await presenter.ReplaceTabBarPanelAsync(request);
        }
        else
        {
            await presenter.ShowOverlayAsync(request);
        }
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

        return await navigationService.GoToAsync(navigation);
    }

    private int _rootSelectionInFlight;

    /// <summary>
    /// Raised when <see cref="IsRootSelectionInFlight"/> flips — every root's
    /// <see cref="ScaffoldRoot.SelectCommand"/> re-evaluates <c>CanExecute</c> on it.
    /// </summary>
    internal event EventHandler? RootSelectionInFlightChanged;

    /// <summary>Gets whether a chrome-initiated root selection is currently navigating.</summary>
    internal bool IsRootSelectionInFlight => _rootSelectionInFlight != 0;

    /// <summary>
    /// <see cref="SelectRootAsync"/> behind the scaffold-wide selection gate: while one
    /// selection navigates, further ones are ignored and every root's
    /// <see cref="ScaffoldRoot.SelectCommand"/> reports non-executable — a second tab can't
    /// race the first (the engine would silently ignore it; the gate makes the UI honest).
    /// Always re-opens on settle (success, guard-cancel or failure).
    /// </summary>
    internal async Task<bool> SelectRootGatedAsync(ScaffoldRoot root)
    {
        if (Interlocked.CompareExchange(ref _rootSelectionInFlight, 1, 0) != 0)
        {
            return false;
        }

        RootSelectionInFlightChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            return await SelectRootAsync(root);
        }
        finally
        {
            Interlocked.Exchange(ref _rootSelectionInFlight, 0);
            RootSelectionInFlightChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the page currently on top of the presented root's stack.</summary>
    internal Page? CurrentDisplayedPage
    {
        get
        {
            var stack = (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack;

            return stack is null ? null
                : stack.PushedPages.Count > 0 ? stack.PushedPages[^1].Page
                : stack.RootPage;
        }
    }

    /// <summary>
    /// Resolves a per-side flyout attached property as a STACK of overrides: the topmost
    /// stack page that explicitly SET the property wins (an explicit null/Disabled overrides
    /// downward), then older pushed pages, the root page, the current area and finally the
    /// scaffold — so a page's drawer survives pushes that don't override it, and a pop
    /// restores the previous page's drawer.
    /// </summary>
    private object? ResolveFlyoutValue(BindableProperty property)
    {
        if ((Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack is { } stack)
        {
            for (var i = stack.PushedPages.Count - 1; i >= 0; i--)
            {
                if (stack.PushedPages[i].Page is { } page && page.IsSet(property))
                {
                    return page.GetValue(property);
                }
            }

            if (stack.RootPage is { } rootPage && rootPage.IsSet(property))
            {
                return rootPage.GetValue(property);
            }
        }

        if (CurrentArea is { } area && area.IsSet(property))
        {
            return area.GetValue(property);
        }

        return GetValue(property);
    }

    internal View? ResolveFlyoutContent(ScaffoldFlyoutSide side)
        => ResolveFlyoutValue(side == ScaffoldFlyoutSide.Start ? FlyoutStartProperty : FlyoutEndProperty) as View;

    internal ScaffoldFlyoutMode ResolveFlyoutMode(ScaffoldFlyoutSide side)
        => (ScaffoldFlyoutMode)ResolveFlyoutValue(side == ScaffoldFlyoutSide.Start ? FlyoutStartModeProperty : FlyoutEndModeProperty)!;

    /// <summary>
    /// Releases the flyout content a page carried when the page leaves the navigation stack:
    /// detaching the logical child clears the inherited BindingContext (the page model must not
    /// be retained through the drawer view) and the handlers of a previously presented drawer
    /// are disconnected. Navigation closes any open overlay before the stack mutates, so the
    /// view is never presented when this runs.
    /// </summary>
    internal static void CleanupPageFlyoutContent(Page page)
    {
        Cleanup(page, FlyoutStartProperty);
        Cleanup(page, FlyoutEndProperty);

        static void Cleanup(Page page, BindableProperty property)
        {
            if (page.IsSet(property) && page.GetValue(property) is View view)
            {
                page.RemoveLogicalChild(view);
                view.DisconnectHandlers();
            }
        }
    }

    /// <summary>
    /// Whether the drawer on the given side exists right now: content resolves non-null AND its
    /// mode allows it (<see cref="ScaffoldFlyoutMode.Auto"/> = stack roots only). The nav bar's
    /// drawer button and <see cref="OpenFlyoutAsync"/> key off the same check.
    /// </summary>
    internal bool ComputeFlyoutAvailable(ScaffoldFlyoutSide side)
    {
        if (ResolveFlyoutContent(side) is null)
        {
            return false;
        }

        return ResolveFlyoutMode(side) switch
        {
            ScaffoldFlyoutMode.Flyout => true,
            ScaffoldFlyoutMode.Auto =>
                (Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack.PushedPages.Count == 0,
            _ => false
        };
    }

    /// <summary>Gets the effective styling for the given drawer side (never null).</summary>
    internal ScaffoldFlyoutOptions GetEffectiveFlyoutOptions(ScaffoldFlyoutSide side)
        => (side == ScaffoldFlyoutSide.Start ? FlyoutStartOptions : FlyoutEndOptions) ?? ScaffoldFlyoutOptions._default;

    /// <summary>
    /// Whether the scaffold renders right-to-left — the presenters map the logical Start/End
    /// drawer sides to physical edges through this single flag (placement, slide direction and,
    /// later, the gesture edge).
    /// </summary>
    internal bool IsRightToLeft
        => ((IVisualElementController)this).EffectiveFlowDirection.HasFlag(EffectiveFlowDirection.RightToLeft);

    /// <summary>Called by the presenter when a drawer finished opening.</summary>
    internal void OnFlyoutPresented(ScaffoldFlyoutSide side)
    {
        if (side == ScaffoldFlyoutSide.Start)
        {
            SetValue(_isFlyoutStartOpenPropertyKey, true);
            FlyoutStartOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SetValue(_isFlyoutEndOpenPropertyKey, true);
            FlyoutEndOpened?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Called by the presenter when a drawer finished closing.</summary>
    internal void OnFlyoutDismissed(ScaffoldFlyoutSide side)
    {
        if (side == ScaffoldFlyoutSide.Start)
        {
            SetValue(_isFlyoutStartOpenPropertyKey, false);
            FlyoutStartClosed?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SetValue(_isFlyoutEndOpenPropertyKey, false);
            FlyoutEndClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the navigation bar title view attached to a page.</summary>
    public static View? GetTitleView(BindableObject bindable) => (View?)bindable.GetValue(TitleViewProperty);

    /// <summary>Sets the navigation bar title view attached to a page.</summary>
    public static void SetTitleView(BindableObject bindable, View? value) => bindable.SetValue(TitleViewProperty, value);

    /// <summary>Gets the navigation bar view attached to an element.</summary>
    public static View? GetNavBarView(BindableObject bindable) => (View?)bindable.GetValue(NavBarViewProperty);

    /// <summary>Sets the navigation bar view attached to an element.</summary>
    public static void SetNavBarView(BindableObject bindable, View? value) => bindable.SetValue(NavBarViewProperty, value);

    /// <summary>Gets the nav bar appearance attached to an element.</summary>
    public static ScaffoldNavBarAppearance? GetNavBarAppearance(BindableObject bindable) => (ScaffoldNavBarAppearance?)bindable.GetValue(NavBarAppearanceProperty);

    /// <summary>Sets the nav bar appearance attached to an element.</summary>
    public static void SetNavBarAppearance(BindableObject bindable, ScaffoldNavBarAppearance? value) => bindable.SetValue(NavBarAppearanceProperty, value);

    /// <summary>
    /// The attached appearance inherits its element's binding context (the same treatment MAUI
    /// gives <see cref="VisualElement.Shadow"/>) so its properties can be bound to page state.
    /// The handler subscription is idempotent (remove-then-add) and dropped when cleared.
    /// </summary>
    private static void OnNavBarAppearanceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Element element)
        {
            return;
        }

        element.BindingContextChanged -= OnAppearanceHostBindingContextChanged;

        if (oldValue is ScaffoldNavBarAppearance previous && ReferenceEquals(previous.Parent, element))
        {
            element.RemoveLogicalChild(previous);
        }

        if (newValue is ScaffoldNavBarAppearance appearance)
        {
            // As a logical child the appearance (and its brush) sits in the element tree, where
            // MAUI delivers theme/resource changes; a style-shared instance keeps its first parent.
            if (appearance.Parent is null)
            {
                element.AddLogicalChild(appearance);
            }

            SetInheritedBindingContext(appearance, element.BindingContext);
            element.BindingContextChanged += OnAppearanceHostBindingContextChanged;
        }
    }

    private static void OnAppearanceHostBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is Element element && GetNavBarAppearance(element) is { } appearance)
        {
            SetInheritedBindingContext(appearance, element.BindingContext);
        }
    }

    /// <summary>
    /// The appearance chain for the given page, most specific first — each appearance property
    /// resolves independently through it (see <see cref="ScaffoldNavBarAppearance.Resolve{T}"/>).
    /// </summary>
    internal (ScaffoldNavBarAppearance? Page, ScaffoldNavBarAppearance? Area, ScaffoldNavBarAppearance? Scaffold) GetNavBarAppearanceChain(Page? currentPage)
        => (
            currentPage is null ? null : GetNavBarAppearance(currentPage),
            CurrentArea is { } area ? GetNavBarAppearance(area) : null,
            GetNavBarAppearance(this)
        );

    /// <summary>Gets whether the navigation bar is visible for a page.</summary>
    public static bool GetIsNavBarVisible(BindableObject bindable) => (bool)bindable.GetValue(IsNavBarVisibleProperty);

    /// <summary>Sets whether the navigation bar is visible for a page.</summary>
    public static void SetIsNavBarVisible(BindableObject bindable, bool value) => bindable.SetValue(IsNavBarVisibleProperty, value);

    /// <summary>Gets the declared system bar icon style of an element.</summary>
    public static ScaffoldSystemBarStyle GetSystemBarStyle(BindableObject bindable) => (ScaffoldSystemBarStyle)bindable.GetValue(SystemBarStyleProperty);

    /// <summary>Sets the declared system bar icon style of an element.</summary>
    public static void SetSystemBarStyle(BindableObject bindable, ScaffoldSystemBarStyle value) => bindable.SetValue(SystemBarStyleProperty, value);

    /// <summary>Gets whether the nav bar draws over the page instead of insetting it.</summary>
    public static bool GetNavBarOverlapsContent(BindableObject bindable) => (bool)bindable.GetValue(NavBarOverlapsContentProperty);

    /// <summary>Gets the declared soft-keyboard mode of a surface (null = unset, resolves to <see cref="ScaffoldKeyboardMode.Resize"/>).</summary>
    public static ScaffoldKeyboardMode? GetKeyboardMode(BindableObject bindable) => (ScaffoldKeyboardMode?)bindable.GetValue(KeyboardModeProperty);

    /// <summary>Sets the declared soft-keyboard mode of a surface.</summary>
    public static void SetKeyboardMode(BindableObject bindable, ScaffoldKeyboardMode? value) => bindable.SetValue(KeyboardModeProperty, value);

    /// <summary>The effective keyboard mode of a page: page → scaffold → <see cref="ScaffoldKeyboardMode.Resize"/>.</summary>
    internal ScaffoldKeyboardMode ResolvePageKeyboardMode(Page? page)
        => (page is null ? null : GetKeyboardMode(page)) ?? GetKeyboardMode(this) ?? ScaffoldKeyboardMode.Resize;

    /// <summary>Sets whether the nav bar draws over the page instead of insetting it.</summary>
    public static void SetNavBarOverlapsContent(BindableObject bindable, bool value) => bindable.SetValue(NavBarOverlapsContentProperty, value);

    /// <summary>Gets the tracked scrollable attached to a page.</summary>
    public static View? GetScrollTracker(BindableObject bindable) => (View?)bindable.GetValue(ScrollTrackerProperty);

    /// <summary>Sets the tracked scrollable attached to a page.</summary>
    public static void SetScrollTracker(BindableObject bindable, View? value) => bindable.SetValue(ScrollTrackerProperty, value);

    /// <summary>Gets the default interpolation ramp start attached to an element.</summary>
    public static double GetScrollRampStart(BindableObject bindable) => (double)bindable.GetValue(ScrollRampStartProperty);

    /// <summary>Sets the default interpolation ramp start attached to an element.</summary>
    public static void SetScrollRampStart(BindableObject bindable, double value) => bindable.SetValue(ScrollRampStartProperty, value);

    /// <summary>Gets the default interpolation ramp end attached to an element.</summary>
    public static double GetScrollRampEnd(BindableObject bindable) => (double)bindable.GetValue(ScrollRampEndProperty);

    /// <summary>Sets the default interpolation ramp end attached to an element.</summary>
    public static void SetScrollRampEnd(BindableObject bindable, double value) => bindable.SetValue(ScrollRampEndProperty, value);

    /// <summary>
    /// Resolves the effective default ramp for the given page, most specific set value wins:
    /// page → current area → scaffold → [0, 100].
    /// </summary>
    internal (double RampStart, double RampEnd) ResolveScrollRamp(Page? currentPage)
        => (ResolveRampValue(ScrollRampStartProperty, currentPage), ResolveRampValue(ScrollRampEndProperty, currentPage));

    private double ResolveRampValue(BindableProperty property, Page? currentPage)
    {
        if (currentPage is not null && currentPage.IsSet(property))
        {
            return (double)currentPage.GetValue(property);
        }

        if (CurrentArea is { } area && area.IsSet(property))
        {
            return (double)area.GetValue(property);
        }

        return (double)GetValue(property);
    }

    /// <summary>Gets the start-drawer button policy attached to an element.</summary>
    public static ScaffoldFlyoutButtonVisibility GetFlyoutStartButtonVisibility(BindableObject bindable) => (ScaffoldFlyoutButtonVisibility)bindable.GetValue(FlyoutStartButtonVisibilityProperty);

    /// <summary>Sets the start-drawer button policy attached to an element.</summary>
    public static void SetFlyoutStartButtonVisibility(BindableObject bindable, ScaffoldFlyoutButtonVisibility value) => bindable.SetValue(FlyoutStartButtonVisibilityProperty, value);

    /// <summary>Gets the end-drawer button policy attached to an element.</summary>
    public static ScaffoldFlyoutButtonVisibility GetFlyoutEndButtonVisibility(BindableObject bindable) => (ScaffoldFlyoutButtonVisibility)bindable.GetValue(FlyoutEndButtonVisibilityProperty);

    /// <summary>Sets the end-drawer button policy attached to an element.</summary>
    public static void SetFlyoutEndButtonVisibility(BindableObject bindable, ScaffoldFlyoutButtonVisibility value) => bindable.SetValue(FlyoutEndButtonVisibilityProperty, value);

    /// <summary>Gets the presentation mode attached to <paramref name="bindable"/>.</summary>
    public static ScaffoldPageMode GetPageMode(BindableObject bindable) => (ScaffoldPageMode)bindable.GetValue(PageModeProperty);

    /// <summary>Sets the presentation mode attached to <paramref name="bindable"/>.</summary>
    public static void SetPageMode(BindableObject bindable, ScaffoldPageMode value) => bindable.SetValue(PageModeProperty, value);

    /// <summary>Gets the page transition attached to <paramref name="bindable"/>.</summary>
    public static ScaffoldPageTransition? GetPageTransition(BindableObject bindable) => (ScaffoldPageTransition?)bindable.GetValue(PageTransitionProperty);

    /// <summary>Sets the page transition attached to <paramref name="bindable"/>.</summary>
    public static void SetPageTransition(BindableObject bindable, ScaffoldPageTransition? value) => bindable.SetValue(PageTransitionProperty, value);

    /// <summary>
    /// Resolves the transition spec for <paramref name="pushedPage"/>: page-attached value →
    /// modal default (modal-mode pages enter from the bottom) →
    /// scaffold-level value → <see cref="ScaffoldPageTransition.Default"/>.
    /// </summary>
    internal ScaffoldPageTransition ResolvePageTransition(Page pushedPage)
        => GetPageTransition(pushedPage)
            ?? (GetPageMode(pushedPage) != ScaffoldPageMode.Default ? ScaffoldPageTransition.SlideFromBottom : null)
            ?? GetPageTransition(this)
            ?? ScaffoldPageTransition.Default;

    /// <summary>
    /// The transition a ROOT switch travels with. Its choreography is fixed — the presenters
    /// slide neighbouring roots and cross-fade roots in different areas — but the DURATION comes
    /// from the scaffold-level spec, so an app tunes all of its page motion in one place.
    /// A page-attached spec deliberately never applies here: how a pushed page enters says
    /// nothing about how tabs travel.
    /// </summary>
    internal ScaffoldPageTransition ResolveRootSwitchTransition()
        => GetPageTransition(this) is { DurationSeconds: var duration }
            ? ScaffoldPageTransition.Default with { DurationSeconds = duration }
            : ScaffoldPageTransition.Default;

    /// <summary>Gets the shared-element transition name attached to a view.</summary>
    public static string? GetTransitionName(BindableObject bindable) => (string?)bindable.GetValue(TransitionNameProperty);

    /// <summary>Sets the shared-element transition name attached to a view.</summary>
    public static void SetTransitionName(BindableObject bindable, string? value) => bindable.SetValue(TransitionNameProperty, value);

    /// <summary>
    /// Gets the observable state the mounted nav bar view binds to (title, back/drawer button
    /// availability, commands) — the binding context of the default template and of custom
    /// nav bar views alike.
    /// </summary>
    public ScaffoldNavBarContext NavBarContext => field ??= new ScaffoldNavBarContext(this);

    /// <summary>
    /// Gets the observable soft-keyboard state (<see cref="ScaffoldKeyboardState.IsVisible"/>,
    /// <see cref="ScaffoldKeyboardState.Height"/>) fed by the platform keyboard geometry — bind
    /// through <see cref="KeyboardBindingExtension"/> / <see cref="KeyboardBindings"/>.
    /// </summary>
    public ScaffoldKeyboardState KeyboardState => field ??= new ScaffoldKeyboardState();

    /// <summary>The system status/navigation bar icon-style owner (see <see cref="SystemBarStyleProperty"/>).</summary>
    internal ScaffoldSystemBars SystemBars => field ??= new ScaffoldSystemBars(this);

    /// <summary>
    /// Resolves the ambient <see cref="ScaffoldNavBarContext"/> from any element hosted in a
    /// scaffold (walks the logical parents) — the code-behind counterpart of
    /// <see cref="NavBarBindingExtension"/>, e.g. to observe
    /// <see cref="ScaffoldNavBarContext.ScrollOffset"/> for scroll-driven chrome.
    /// Null while the element is not attached to a scaffold's tree yet.
    /// </summary>
    public static ScaffoldNavBarContext? FindNavBarContext(Element? element)
    {
        while (element is not null)
        {
            if (element is Scaffold scaffold)
            {
                return scaffold.NavBarContext;
            }

            element = element.Parent;
        }

        return null;
    }

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
        => GetPageMode(page) == ScaffoldPageMode.Default // a modal page always covers the tab bar
            && GetTabBarVisibility(page) switch
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
    /// Views hosted by the scaffold as logical children (popup and sheet content, scrims, drawers,
    /// panels, bar views) are placed by the presenters, not by the page: none of them may be
    /// <see cref="LayoutConstraint.Fixed"/> ("fills the page" — the verdict a <c>TemplatedPage</c>
    /// gives its Fill/Fill children) because a Fixed root stops MAUI's platform
    /// measure-invalidation walk at itself and re-lays out in its CURRENT bounds, so a popup whose
    /// content grows after presentation would never reach the presenter. The nav bar host lives in
    /// a strip that fixes its WIDTH only (its height follows the bar): HorizontallyFixed.
    /// </summary>
    protected override LayoutConstraint ComputeConstraintForView(View view)
        => view is ScaffoldNavBarHost
            ? LayoutConstraint.HorizontallyFixed
            : LayoutConstraint.None;

    /// <summary>
    /// Routes back requests arriving through MAUI's legacy/synthetic channel (e.g. automation
    /// drivers, platforms without dispatcher-based back) into the navigation engine, matching
    /// the behavior of the Android dispatcher callback. Guards and lifecycle always run.
    /// </summary>
    /// <returns>True when a pop was dispatched.</returns>
    protected override bool OnBackButtonPressed()
    {
        // Overlays dismiss (topmost first) before the navigation engine is ever consulted —
        // the same policy §7.2 defines for popups.
        if (Presenter is { HasOverlay: true } presenter)
        {
            Dispatcher.Dispatch(() => presenter.CloseTopOverlayAsync().FireAndForget(Handler));

            return true;
        }

        if (NavigationService is { } navigationService
            && Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy rootProxy
            && rootProxy.Root.NavigationStack.PushedPages.Count > 0)
        {
            // A plain Modal blocks system back on every channel — same rule as the Android
            // dispatcher callback: the press is consumed without popping.
            if (GetPageMode(rootProxy.Root.NavigationStack.PushedPages[^1].Page) == ScaffoldPageMode.Modal)
            {
                return true;
            }

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
        var initTask = InitializeAsync(services);

        // Initial display: synchronize the presenter with the startup destination.
        if (Presenter is { } presenter && Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy currentRoot)
        {
            await presenter.SynchronizeAsync(currentRoot.Root, ScaffoldPresentationHint.None);
        }
        
        await initTask;
    }

    /// <summary>True once the navigation engine has been initialized on this scaffold.</summary>
    internal bool IsInitialized => _initialized;

    private bool _areaReconcileScheduled;

    private void OnAreasCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // XAML hot reload re-runs the whole initialization on the LIVE scaffold, re-ADDING the
        // structure: an area added AFTER initialization whose root page types intersect an
        // existing area's REPLACES it (the freshly inflated instance wins). Pre-initialization
        // duplicates are legitimate app structure (the same page type may root multiple
        // stacks) and are left untouched. Deferred to the dispatcher: ObservableCollection
        // forbids mutating from inside its own change event.
        if (!_initialized || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add || _areaReconcileScheduled)
        {
            return;
        }

        _areaReconcileScheduled = true;

        Dispatcher.Dispatch(() =>
        {
            _areaReconcileScheduled = false;
            ReconcileDuplicateAreas();
        });
    }

    private void ReconcileDuplicateAreas()
    {
        for (var i = Areas.Count - 1; i >= 0; i--)
        {
            var area = Areas[i];
            var pageTypes = area.Roots.Select(r => r.PageType).Where(t => t is not null).ToHashSet();

            if (pageTypes.Count == 0)
            {
                continue;
            }

            // The LAST area wins (hot reload appends the fresh structure): remove any EARLIER
            // area sharing a root page type with it.
            for (var j = i - 1; j >= 0; j--)
            {
                if (Areas[j].Roots.Any(r => r.PageType is { } t && pageTypes.Contains(t)))
                {
                    Areas.RemoveAt(j);
                    i--;
                }
            }
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
        return navigationService.InitializeAsync(proxy, initialSegmentName, InitialIntent);
    }

    /// <summary>
    /// Whether a navigation is being executed by the engine right now — from its request through
    /// guards, page swap, transition and the awaited lifecycle callbacks (an <c>OnAppearingAsync</c>
    /// that takes seconds keeps it in flight). Navigations are serialized, so this is a plain flag.
    /// The back PREVIEWS (Android predictive back, iOS interactive pop) do not start while set:
    /// peeking the page below a page that is still arriving would scrub a stack in motion; the back
    /// request itself still reaches the engine, which serializes or ignores it as usual.
    /// </summary>
    internal bool IsNavigationInFlight { get; private set; }

    internal void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args)
    {
        switch (args.EventType)
        {
            case NavigationLifecycleEventType.NavigationRequested:
                IsNavigationInFlight = true;

                break;

            case NavigationLifecycleEventType.NavigationCompleted:
            case NavigationLifecycleEventType.NavigationCanceled:
            case NavigationLifecycleEventType.NavigationFailed:
                IsNavigationInFlight = false;

                break;
        }

        NavigationEvent?.Invoke(this, args);
    }

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
