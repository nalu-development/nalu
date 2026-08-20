using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The binding context of the mounted nav bar view (the default template or a custom
/// replacement): one observable object PER PAGE, created when the page enters a navigation
/// stack and live while that page's title, title view, binding context, scroll ramp or
/// flyout policies change. The page it describes never changes — during a transition two
/// contexts are alive and each bar shows its OWN page's state.
/// Custom bars bind to it directly (e.g. <c>IsVisible="{Binding CanNavigateBack}"</c>,
/// <c>Command="{Binding BackCommand}"</c>) — the same contract the built-in
/// <see cref="ScaffoldBackButton"/>, <see cref="ScaffoldFlyoutButton"/> and
/// <see cref="ScaffoldNavBarTitle"/> primitives consume.
/// </summary>
public sealed class ScaffoldNavBarContext : INotifyPropertyChanged
{
    private readonly Scaffold _scaffold;
    private readonly ScaffoldRoot? _root;

    /// <summary>The page this context describes; null only for the scaffold's detached fallback context.</summary>
    internal Page? Page { get; }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the current page's <see cref="Page.Title"/>.</summary>
    public string? Title
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>Gets the current page's <see cref="Scaffold.TitleViewProperty"/> content, replacing the title label when set.</summary>
    public View? TitleView
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the page currently on top of the presented stack — the escape hatch for custom
    /// bars binding page-specific state (e.g. <c>CurrentPage.BindingContext.SomeCommand</c>;
    /// such paths are reflection-mode bindings, not compilable).
    /// </summary>
    public Page? CurrentPage
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the current page's binding context: the title slot propagates it to hosted
    /// <see cref="TitleView"/> content, which is page content and binds the page model —
    /// not this context.
    /// </summary>
    public object? PageBindingContext
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the effective <see cref="ScaffoldNavBarAppearance.Foreground"/>: the color
    /// fallback of every primitive (title text, glyphs) — a color set directly or via style
    /// on a primitive wins over it. Null when no appearance in the chain sets one, in which
    /// case primitives use their built-in default color.
    /// </summary>
    public Color? Foreground
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the effective title color from the appearance chain: level by level, the first
    /// appearance setting <see cref="ScaffoldNavBarAppearance.TitleForeground"/> or
    /// <see cref="ScaffoldNavBarAppearance.Foreground"/> wins (its title color first). Null when
    /// no appearance sets either — the title then uses its built-in default.
    /// </summary>
    public Color? TitleForeground
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the tracked scrollable's vertical offset in dp (see
    /// <see cref="Scaffold.ScrollTrackerProperty"/>): 0 at rest, negative while
    /// over-scrolling at the top. Updates per frame — bind chrome transforms to it
    /// (e.g. via <see cref="NavBarBindingExtension"/> and a converter).
    /// </summary>
    public double ScrollOffset
    {
        get;
        internal set
        {
            SetField(ref field, value);
            IsScrolledUnder = value > 0.5;
        }
    }

    /// <summary>Gets whether the tracked content is scrolled down (offset above 0.5dp).</summary>
    public bool IsScrolledUnder
    {
        get;
        private set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the effective interpolation ramp start for the current page (resolved
    /// page → area → scaffold via <see cref="Scaffold.ScrollRampStartProperty"/>; 0 by
    /// default): the <c>RampStart</c> fallback of every scroll-value interpolation on the page.
    /// </summary>
    public double ScrollRampStart
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets the effective interpolation ramp end for the current page (resolved
    /// page → area → scaffold via <see cref="Scaffold.ScrollRampEndProperty"/>; 100 by default).
    /// </summary>
    public double ScrollRampEnd
    {
        get;
        internal set => SetField(ref field, value);
    } = 100;

    /// <summary>
    /// Gets whether back navigation is offered: the stack has at least one pushed page and the
    /// current page is not modal.
    /// </summary>
    public bool CanNavigateBack
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets whether the start-drawer button should show: flyout content resolves, the page is
    /// not modal, and the <see cref="ScaffoldFlyoutButtonVisibility"/> policy allows it
    /// (by default only at the stack root).
    /// </summary>
    public bool IsFlyoutStartButtonVisible
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets whether the end-drawer button should show: flyout content resolves, the page is
    /// not modal, and the <see cref="ScaffoldFlyoutButtonVisibility"/> policy allows it
    /// (by default only at the stack root).
    /// </summary>
    public bool IsFlyoutEndButtonVisible
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>Gets whether the current page is a modal page (<see cref="Scaffold.PageModeProperty"/>).</summary>
    public bool IsModal
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Gets whether the trailing close (X) button should show:
    /// <see cref="ScaffoldPageMode.DismissableModal"/> pages.
    /// </summary>
    public bool IsCloseButtonVisible
    {
        get;
        internal set => SetField(ref field, value);
    }

    /// <summary>
    /// Pops the current page through the navigation engine — guards and lifecycle run.
    /// Cannot execute while a pop is already in flight.
    /// </summary>
    public ICommand BackCommand { get; }

    /// <summary>Opens the start-edge flyout.</summary>
    public ICommand OpenFlyoutStartCommand { get; }

    /// <summary>Opens the end-edge flyout.</summary>
    public ICommand OpenFlyoutEndCommand { get; }

    internal ScaffoldNavBarContext(Scaffold scaffold, Page? page = null, ScaffoldRoot? root = null)
    {
        _scaffold = scaffold;
        Page = page;
        _root = root;

        if (page is not null)
        {
            // The observed page is fixed for this context's life: no re-targeting, so a page
            // leaving the screen keeps reporting its own state while it animates away.
            page.PropertyChanged += OnPagePropertyChanged;
        }

        // Non-reentrant commands: CanExecute stays false while the operation is in flight, so
        // fast repeated taps can't queue duplicate pops / flyout openings.
        BackCommand = new NaluAsyncCommand(
            () => scaffold.NavigationService is { } navigationService
                ? scaffold.Dispatcher.DispatchAsync(() => navigationService.GoToAsync(Navigation.Relative().Pop()))
                : Task.CompletedTask,
            () => scaffold.Handler
        );

        OpenFlyoutStartCommand = new NaluAsyncCommand(() => scaffold.OpenFlyoutAsync(ScaffoldFlyoutSide.Start), () => scaffold.Handler);
        OpenFlyoutEndCommand = new NaluAsyncCommand(() => scaffold.OpenFlyoutAsync(ScaffoldFlyoutSide.End), () => scaffold.Handler);
    }

    /// <summary>
    /// Recomputes every value from the current navigation state for THIS context's page.
    /// Invoked when the page enters the stack and by the presenters for the incoming page on
    /// each synchronization (stack-dependent values — back/close/drawer buttons — change with
    /// the stack, not with the page); page-level changes (Title, TitleView, flyout policies)
    /// are observed live.
    /// </summary>
    internal void Refresh()
    {
        if (Page is not { } currentPage)
        {
            return;
        }

        var stackEmpty = (_root?.NavigationStack.PushedPages.Count ?? 0) == 0;
        var pageMode = Scaffold.GetPageMode(currentPage);
        var isModal = pageMode != ScaffoldPageMode.Default;

        Title = currentPage.Title;
        CurrentPage = currentPage;
        PageBindingContext = currentPage.BindingContext;
        TitleView = Scaffold.GetTitleView(currentPage);
        (ScrollRampStart, ScrollRampEnd) = _scaffold.ResolveScrollRamp(currentPage);
        IsModal = isModal;
        IsCloseButtonVisible = pageMode == ScaffoldPageMode.DismissableModal;

        // A modal page shows title + close only: no back chevron, no drawer buttons.
        CanNavigateBack = !stackEmpty && !isModal;

        IsFlyoutStartButtonVisible = !isModal && ComputeFlyoutButtonVisible(
            _scaffold.ComputeFlyoutAvailable(ScaffoldFlyoutSide.Start),
            ResolveFlyoutButtonVisibility(currentPage, Scaffold.FlyoutStartButtonVisibilityProperty),
            stackEmpty
        );

        IsFlyoutEndButtonVisible = !isModal && ComputeFlyoutButtonVisible(
            _scaffold.ComputeFlyoutAvailable(ScaffoldFlyoutSide.End),
            ResolveFlyoutButtonVisibility(currentPage, Scaffold.FlyoutEndButtonVisibilityProperty),
            stackEmpty
        );
    }

    private static bool ComputeFlyoutButtonVisible(bool contentResolves, ScaffoldFlyoutButtonVisibility visibility, bool stackEmpty)
        => contentResolves && visibility switch
        {
            ScaffoldFlyoutButtonVisibility.Visible => true,
            ScaffoldFlyoutButtonVisibility.Hidden => false,
            _ => stackEmpty
        };

    /// <summary>Most specific non-default wins: current Page → current ScaffoldArea → Scaffold.</summary>
    private ScaffoldFlyoutButtonVisibility ResolveFlyoutButtonVisibility(Page currentPage, BindableProperty property)
    {
        if (currentPage.IsSet(property))
        {
            return (ScaffoldFlyoutButtonVisibility)currentPage.GetValue(property);
        }

        if (_scaffold.CurrentArea is { } area && area.IsSet(property))
        {
            return (ScaffoldFlyoutButtonVisibility)area.GetValue(property);
        }

        return (ScaffoldFlyoutButtonVisibility)_scaffold.GetValue(property);
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Page page || !ReferenceEquals(page, Page))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(Page.Title):
                Title = page.Title;

                break;

            case "TitleView":
                TitleView = Scaffold.GetTitleView(page);

                break;

            case nameof(Page.BindingContext):
                PageBindingContext = page.BindingContext;

                break;

            case "ScrollRampStart":
            case "ScrollRampEnd":
                (ScrollRampStart, ScrollRampEnd) = _scaffold.ResolveScrollRamp(page);

                break;

            case "FlyoutStartButtonVisibility":
            case "FlyoutEndButtonVisibility":
            case "FlyoutStartMode":
            case "FlyoutEndMode":
                Refresh();

                break;
        }
    }

    /// <summary>
    /// Releases everything this context held once its page is gone for good: the page
    /// subscription AND every field that references the page or its model.
    /// </summary>
    /// <remarks>
    /// Dropping the references matters as much as unsubscribing. This context is reachable from
    /// objects that outlive the page — a bar host subscribes to the scaffold and the area, a
    /// binding relay is held by the ancestors it walked — and it holds the page
    /// (<see cref="CurrentPage"/>), the page's MODEL (<see cref="PageBindingContext"/>) and the
    /// page's <see cref="TitleView"/>. Left set, any one of those keeps a dead screen's whole
    /// object graph alive.
    /// </remarks>
    internal void Detach()
    {
        if (Page is not null)
        {
            Page.PropertyChanged -= OnPagePropertyChanged;
        }

        CurrentPage = null;
        PageBindingContext = null;
        TitleView = null;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for every property (the scaffold-level forwarder swap).</summary>
    internal void RaiseAllChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
