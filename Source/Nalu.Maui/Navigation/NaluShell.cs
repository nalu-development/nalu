namespace Nalu;

using System.ComponentModel;
using Nalu.Internals;

/// <summary>
/// Nalu shell, the shell navigation you wanted.
/// </summary>
public abstract class NaluShell : Shell
{
    private readonly AsyncLocal<bool> _isNavigating = new();
    private INavigationServiceInternal? _navigationService;
    private INavigationOptions? _navigationOptions;

    internal void SetIsNavigating(bool value) => _isNavigating.Value = value;

    /// <inheritdoc />
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
        if (!_isNavigating.Value)
        {
            _ = args.Cancel();

            if (Handler is null || _navigationService is null || _navigationOptions is null)
            {
                return;
            }

            var mappings = _navigationOptions.Mapping;
            var segmentName = args.Target.Location.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
            if (mappings.FirstOrDefault(map => NavigationHelper.GetSegmentName(map.Key) == segmentName) is not { Key: { } pageModelType })
            {
                throw new KeyNotFoundException($"Unable to find page model type for {args.Target.Location.OriginalString}.");
            }

            var absoluteNavigation = Nalu.Navigation.Absolute();
            absoluteNavigation.Add(new NavigationSegment(pageModelType));
            _ = _navigationService.GoToAsync(absoluteNavigation);
        }
    }

    /// <summary>
    /// Initializes with <typeparamref name="TPageModel"/> as root page.
    /// </summary>
    /// <param name="intent">The intent to .</param>
    /// <typeparam name="TPageModel">Type of the page model for the root page.</typeparam>
    protected void ConfigureNavigation<TPageModel>(object? intent = null)
        where TPageModel : INotifyPropertyChanged
    {
        HandlerChanged += OnHandlerSet;

#pragma warning disable VSTHRD100
        async void OnHandlerSet(object? sender, EventArgs e)
#pragma warning restore VSTHRD100
        {
            if (Handler is null)
            {
                return;
            }

            HandlerChanged -= OnHandlerSet;

            var serviceProvider = Handler!.GetServiceProvider();
            _navigationService = serviceProvider.GetService<INavigationServiceInternal>() ??
                                    throw new InvalidOperationException("MauiAppBuilder must be configured with UseNaluNavigation().");
            _navigationOptions = serviceProvider.GetRequiredService<INavigationOptions>();

            var controller = new ShellNavigationController(_navigationService, _navigationOptions, this);
            await _navigationService.InitializeAsync<TPageModel>(controller, intent).ConfigureAwait(true);
        }
    }
}
