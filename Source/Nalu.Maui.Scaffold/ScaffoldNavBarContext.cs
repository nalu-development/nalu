using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The binding context of the mounted nav bar view (the default template or a custom
/// replacement): one observable object per <see cref="Scaffold"/>, updated on every navigation
/// and live while the current page's title, title view, binding context, scroll ramp or
/// flyout policies change.
/// Custom bars bind to it directly (e.g. <c>IsVisible="{Binding CanNavigateBack}"</c>,
/// <c>Command="{Binding BackCommand}"</c>) — the same contract the built-in
/// <see cref="ScaffoldBackButton"/>, <see cref="ScaffoldFlyoutButton"/> and
/// <see cref="ScaffoldNavBarTitle"/> primitives consume.
/// </summary>
public sealed class ScaffoldNavBarContext : INotifyPropertyChanged
{
    private readonly Scaffold _scaffold;
    private Page? _observedPage;

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

    internal ScaffoldNavBarContext(Scaffold scaffold)
    {
        _scaffold = scaffold;

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
    /// Recomputes every value from the current navigation state. Invoked by the presenter on
    /// each synchronization; page-level changes (Title, TitleView, flyout policies) are
    /// observed live on the current page.
    /// </summary>
    internal void Update(ScaffoldRoot root, Page currentPage)
    {
        if (!ReferenceEquals(_observedPage, currentPage))
        {
            if (_observedPage is not null)
            {
                _observedPage.PropertyChanged -= OnPagePropertyChanged;
            }

            _observedPage = currentPage;
            currentPage.PropertyChanged += OnPagePropertyChanged;
        }

        var stackEmpty = root.NavigationStack.PushedPages.Count == 0;
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
        if (sender is not Page page || !ReferenceEquals(page, _observedPage))
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
                if (_scaffold.Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy rootProxy)
                {
                    Update(rootProxy.Root, page);
                }

                break;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
