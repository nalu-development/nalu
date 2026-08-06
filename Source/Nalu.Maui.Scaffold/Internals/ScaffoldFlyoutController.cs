namespace Nalu;

/// <summary>
/// <see cref="IScaffoldFlyoutController"/> implementation: resolves the ambient
/// <see cref="Scaffold"/> through the navigation service's shell proxy at call time —
/// no-op while the app is not scaffold-hosted (or not initialized yet).
/// </summary>
internal sealed class ScaffoldFlyoutController(INavigationService navigationService) : IScaffoldFlyoutController
{
    public Task OpenAsync(ScaffoldFlyoutSide side)
        => ResolveScaffold()?.OpenFlyoutAsync(side) ?? Task.CompletedTask;

    public Task CloseAsync()
        => ResolveScaffold()?.CloseFlyoutAsync() ?? Task.CompletedTask;

    private Scaffold? ResolveScaffold()
        => navigationService is NavigationService { ShellProxyOrDefault: ScaffoldProxy proxy } ? proxy.Scaffold : null;
}
