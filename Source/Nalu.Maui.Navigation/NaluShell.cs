namespace Nalu;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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
    protected override async void OnNavigating(ShellNavigatingEventArgs args)
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
                await Task.Yield();
                await Task.Yield();
                await _navigationService.GoToAsync(Nalu.Navigation.Relative().Pop()).ConfigureAwait(true);
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

            await Task.Yield();
            await Task.Yield();
            try
            {
                await _navigationService.GoToAsync(navigation).ConfigureAwait(true);
            }
            catch (InvalidNavigationException ex)
            {
                if (Debugger.IsAttached)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }

    private bool GetIsNavigating() => _isNavigating.Value?.Value ?? false;

    private static readonly Regex _normalizeSegmentRegex = NormalizeSegmentRegex();
    [GeneratedRegex("^(D_FAULT_|IMPL_)")]
    private static partial Regex NormalizeSegmentRegex();
    private static string NormalizeSegment(string segment)
        => _normalizeSegmentRegex.Replace(segment, string.Empty);
}
