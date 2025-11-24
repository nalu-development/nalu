using System.Text.RegularExpressions;
using Nalu.Internals;
// ReSharper disable once RedundantUsingDirective
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Nalu;

#pragma warning disable IDE0290
#pragma warning disable VSTHRD100

/// <summary>
/// Nalu shell, the shell navigation you wanted.
/// </summary>
public abstract partial class NaluShell : Shell, INaluShell, IDisposable
{
    private readonly object? _rootPageIntent;
    private readonly string _rootPageRoute;
    private bool _initialized;
    private ShellProxy? _shellProxy;

    /// <summary>
    /// Occurs when a navigation event is triggered.
    /// </summary>
    public event EventHandler<NavigationLifecycleEventArgs>? NavigationEvent;

    internal NavigationService NavigationService { get; }

    IShellProxy INaluShell.ShellProxy => _shellProxy ?? throw new InvalidOperationException("The shell info is not available yet.");

    /// <summary>
    /// Initializes a new instance of the <see cref="NaluShell" /> class.
    /// </summary>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="rootPageRoute">The custom route used to identify the root shell content.</param>
    /// <param name="rootPageIntent">The optional intent to be provided to the initial root page.</param>
    protected NaluShell(INavigationService navigationService, string rootPageRoute, object? rootPageIntent = null)
    {
        NavigationService = (NavigationService) navigationService;
        _rootPageIntent = rootPageIntent;
        _rootPageRoute = rootPageRoute;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NaluShell" /> class.
    /// </summary>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="rootPageType">The initial root page to be used.</param>
    /// <param name="rootPageIntent">The optional intent to be provided to the initial root page.</param>
    protected NaluShell(INavigationService navigationService, Type rootPageType, object? rootPageIntent = null)
    {
        if (!rootPageType.IsSubclassOf(typeof(Page)))
        {
            throw new ArgumentException("The root page type must be a subclass of Page.", nameof(rootPageType));
        }

        NavigationService = (NavigationService) navigationService;
        _rootPageIntent = rootPageIntent;
        _rootPageRoute = NavigationSegmentAttribute.GetSegmentName(rootPageType);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is not null && !_initialized)
        {
            _shellProxy = new ShellProxy(this);
            _shellProxy.InitializeWithContent(_rootPageRoute);
            _ = NavigationService.InitializeAsync(_shellProxy, _rootPageRoute, _rootPageIntent);
            _initialized = true;
        }
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="NaluShell" />.
    /// </summary>
    /// <param name="disposing">True when disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shellProxy?.Dispose();
        }
    }

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
#if WINDOWS || !(IOS || ANDROID || MACCATALYST)
        var backButtonBehavior = GetBackButtonBehavior(GetVisiblePage());

        if (backButtonBehavior != null)
        {
            var command = backButtonBehavior.GetPropertyIfSet<ICommand>(BackButtonBehavior.CommandProperty, null!);
            var commandParameter = backButtonBehavior.GetPropertyIfSet<object>(BackButtonBehavior.CommandParameterProperty, null!);

            if (command != null)
            {
                command.Execute(commandParameter);

                return true;
            }
        }
#endif

        if (GetVisiblePage() is { } page && page.SendBackButtonPressed())
        {
            return true;
        }

        var currentContent = CurrentItem?.CurrentItem;

        if (currentContent != null && currentContent.Stack.Count > 1)
        {
            DispatchNavigation(Nalu.Navigation.Relative().Pop());

            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Triggered when a navigation is about to happen.
    /// </summary>
    /// <remarks>
    /// Gives the ability to cancel the navigation.
    /// </remarks>
    /// <param name="args"></param>
    protected virtual void OnNavigating(NaluShellNavigatingEventArgs args) { }

    internal void SendOnNavigating(NaluShellNavigatingEventArgs args) => OnNavigating(args);

    /// <inheritdoc />
    protected sealed override void OnNavigating(ShellNavigatingEventArgs args)
    {
        var uri = args.Target.Location.OriginalString;
        var currentUri = args.Current?.Location.OriginalString ?? string.Empty;
        
        if (!_initialized || // Shell initialization process
            Handler is null || // Shell initialization process
            string.IsNullOrEmpty(uri) || // An empty URI is very likely Android trying to background the app when on a root page and back button is pressed.
            CommunityToolkitPopupRegex().IsMatch(uri) || // CommunityToolkit popup navigation
            CommunityToolkitPopupRegex().IsMatch(currentUri) || // CommunityToolkit popup navigation
            IsRemovingStackPages(args) || // ShellSectionProxy removing pages from the stack during cross-item navigation
            uri.EndsWith("?nalu")) // Nalu-triggered navigations
        {
            return;
        }

        args.Cancel();

        if (uri == "..")
        {
            DispatchNavigation(Nalu.Navigation.Relative().Pop());

            return;
        }

        // Only reason we're here is due to shell content navigation from Shell Flyout or Tab bars
        // Now find the ShellContent target and navigate to it via the navigation service
        var segments = uri
                       .Split('/', StringSplitOptions.RemoveEmptyEntries)
                       .Select(NormalizeSegment)
                       .ToArray();

        var shellContent = (ShellContentProxy) _shellProxy!.FindContent(segments);
        var shellSection = shellContent.Parent;

        var ownsNavigationStack = shellSection.CurrentContent == shellContent;
        var navigation = (Navigation) Nalu.Navigation.Absolute();

        navigation.Add(
            new NavigationSegment
            {
                Type = Nalu.Navigation.GetPageType(shellContent.Content),
                SegmentName = shellContent.SegmentName
            }
        );

        if (ownsNavigationStack)
        {
            var navigationStackPages = shellSection.GetNavigationStack().ToArray();
            var segmentsCount = segments.Length;
            var navigationStackCount = navigationStackPages.Length;

            for (var i = 1; i < segmentsCount && i < navigationStackCount; i++)
            {
                var stackPage = navigationStackPages[i];

                navigation.Add(
                    new NavigationSegment
                    {
                        Type = stackPage.Page.GetType(),
                        SegmentName = stackPage.SegmentName
                    }
                );
            }
        }

        DispatchNavigation(navigation);
    }

    private bool IsRemovingStackPages(ShellNavigatingEventArgs args)
    {
        if (args.Source is not ShellNavigationSource.Remove)
        {
            return false;
        }

        var segments = args.Target.Location.OriginalString
                           .Split('/', StringSplitOptions.RemoveEmptyEntries)
                           .Select(NormalizeSegment)
                           .ToArray();

        var shellContent = (ShellContentProxy) _shellProxy!.FindContent(segments);
        var shellSection = shellContent.Parent;

        // If the ShellContent relative to a stack page being removed does not have a page,
        // it means this can only be Nalu navigation cleaning up the stack after a cross-item navigation.
        // If that's not null, then check if any of the pages in the stack is marked for removal.
        var isRemovingStackPages = shellContent.Page is null ||
                                   shellSection.GetNavigationStack(shellContent).Any(stackPage => ShellSectionProxy.IsPageMarkedForRemoval(stackPage.Page));

        return isRemovingStackPages;
    }

    internal void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args) => NavigationEvent?.Invoke(this, args);

    private void DispatchNavigation(INavigationInfo navigation) =>
        Dispatcher.Dispatch(() => NavigationService.GoToAsync(navigation).FireAndForget(Handler));

    private Page? GetVisiblePage()
    {
        if (CurrentItem?.CurrentItem is IShellSectionController scc)
        {
            return scc.PresentedPage;
        }

        return null;
    }

    [GeneratedRegex("^(D_FAULT_|IMPL_)")]
    private static partial Regex NormalizeSegmentRegex();

    private static string NormalizeSegment(string segment)
        => NormalizeSegmentRegex().Replace(segment, string.Empty);

    // See: https://github.com/CommunityToolkit/Maui/blob/main/src/CommunityToolkit.Maui/Extensions/PopupExtensions.shared.cs#L165
    // We need to match: $"{nameof(PopupPage)}" + Guid.NewGuid();
    // In example: "PopupPageca6500ff-c430-49d4-9f79-f5536f71f959";
    [GeneratedRegex(@"\bPopupPage[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase)]
    private static partial Regex CommunityToolkitPopupRegex();

    /// <summary>
    /// Gets or sets the custom tab bar view to be used for the current <see cref="ShellItem" /> (TabBar or FlyoutItem).
    /// </summary>
    public static readonly BindableProperty TabBarViewProperty = BindableProperty.CreateAttached(
        "TabBarView",
        typeof(View),
        typeof(NaluShell),
        null
    );

    /// <summary>
    /// Gets the custom tab bar view to be used for the current <see cref="ShellItem" />.
    /// </summary>
    /// <param name="bindable">The <see cref="ShellItem" /> (TabBar or FlyoutItem).</param>
    public static View? GetTabBarView(BindableObject bindable) => (View?) bindable.GetValue(TabBarViewProperty);

    /// <summary>
    /// Sets the custom tab bar view to be used for the current <see cref="ShellItem" />.
    /// </summary>
    /// <param name="bindable">The <see cref="ShellItem" /> (TabBar or FlyoutItem).</param>
    /// <param name="value">The custom tab bar view.</param>
    public static void SetTabBarView(BindableObject bindable, View? value)
    {
        if (bindable is not ShellItem)
        {
            throw new InvalidOperationException("TabBarView can only be attached to ShellItem (TabBar or FlyoutItem).");
        }

        bindable.SetValue(TabBarViewProperty, value);
    }
}
