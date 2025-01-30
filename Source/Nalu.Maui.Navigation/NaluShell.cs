namespace Nalu;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Nalu.Internals;

#pragma warning disable IDE0290
#pragma warning disable VSTHRD100

/// <summary>
/// Nalu shell, the shell navigation you wanted.
/// </summary>
public abstract partial class NaluShell : Shell, INaluShell, IDisposable
{
    private readonly NavigationService _navigationService;
    private readonly object? _rootPageIntent;
    private readonly string _rootPageRoute;
    private readonly AsyncLocal<StrongBox<bool>> _isNavigating = new();
    private bool _initialized;
    private ShellProxy? _shellProxy;

    IShellProxy INaluShell.ShellProxy => _shellProxy ?? throw new InvalidOperationException("The shell info is not available yet.");

    /// <summary>
    /// Initializes a new instance of the <see cref="NaluShell"/> class.
    /// </summary>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="rootPageRoute">The custom route used to identify the root shell content.</param>
    /// <param name="rootPageIntent">The optional intent to be provided to the initial root page.</param>
    protected NaluShell(INavigationService navigationService, string rootPageRoute, object? rootPageIntent = null)
    {
        _navigationService = (NavigationService)navigationService;
        _rootPageIntent = rootPageIntent;
        _rootPageRoute = rootPageRoute;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NaluShell"/> class.
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

        _navigationService = (NavigationService)navigationService;
        _rootPageIntent = rootPageIntent;
        _rootPageRoute = NavigationSegmentAttribute.GetSegmentName(rootPageType);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal void SetIsNavigating(bool value)
    {
        if (_isNavigating.Value is { } isNavigating)
        {
            isNavigating.Value = value;
            return;
        }

        _isNavigating.Value ??= new StrongBox<bool>(value);
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="NaluShell"/>.
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
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null || _initialized)
        {
            return;
        }

        _initialized = true;
        _shellProxy = new ShellProxy(this);
        _ = _navigationService.InitializeAsync(_shellProxy, _rootPageRoute, _rootPageIntent);
    }

    /// <inheritdoc />
    protected override bool OnBackButtonPressed()
    {
#if WINDOWS || !(IOS || ANDROID || MACCATALYST)
        var backButtonBehavior = GetBackButtonBehavior(GetVisiblePage());
        if (backButtonBehavior != null)
        {
            var command = backButtonBehavior.GetPropertyIfSet<System.Windows.Input.ICommand>(BackButtonBehavior.CommandProperty, null!);
            var commandParameter = backButtonBehavior.GetPropertyIfSet<object>(BackButtonBehavior.CommandParameterProperty, null!);

            if (command != null)
            {
                command.Execute(commandParameter);
                return true;
            }
        }
#endif

        if (GetVisiblePage() is Page page && page.SendBackButtonPressed())
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

    /// <inheritdoc />
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        if (!GetIsNavigating())
        {
            var uri = args.Target.Location.OriginalString;
            if (Handler is null || string.IsNullOrEmpty(uri))
            {
                // We have nothing to do here.
                // On android this may lead to backgrounding the app when on a root page and back button is pressed.
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
            var shellContent = (ShellContentProxy)_shellProxy!.FindContent(segments);
            var shellSection = shellContent.Parent;

            var ownsNavigationStack = shellSection.CurrentContent == shellContent;
            var navigation = (Navigation)Nalu.Navigation.Absolute();

            navigation.Add(new NavigationSegment
            {
                Type = Nalu.Navigation.GetPageType(shellContent.Content),
                SegmentName = shellContent.SegmentName,
            });

            if (ownsNavigationStack)
            {
                var navigationStackPages = shellSection.GetNavigationStack().ToArray();
                var segmentsCount = segments.Length;
                var navigationStackCount = navigationStackPages.Length;
                for (var i = 1; i < segmentsCount && i < navigationStackCount; i++)
                {
                    var stackPage = navigationStackPages[i];
                    navigation.Add(new NavigationSegment
                    {
                        Type = stackPage.Page.GetType(),
                        SegmentName = stackPage.SegmentName,
                    });
                }
            }

            DispatchNavigation(navigation);
        }
    }

    private void DispatchNavigation(INavigationInfo navigation) =>
        Dispatcher.Dispatch(() => _navigationService.GoToAsync(navigation).FireAndForget(Handler));

    private Page? GetVisiblePage()
    {
        if (CurrentItem?.CurrentItem is IShellSectionController scc)
        {
            return scc.PresentedPage;
        }

        return null;
    }

    private bool GetIsNavigating() => _isNavigating.Value?.Value ?? false;

    [GeneratedRegex("^(D_FAULT_|IMPL_)")]
    private static partial Regex NormalizeSegmentRegex();

    private static string NormalizeSegment(string segment)
        => NormalizeSegmentRegex().Replace(segment, string.Empty);
}
