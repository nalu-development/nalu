namespace Nalu;

/// <summary>
/// Page-scope service opening and closing the ambient <see cref="Scaffold"/>'s drawers —
/// page models control the flyout without a scaffold reference. Registered by
/// <c>UseNaluScaffold()</c>; every call is a no-op when the app is not hosted in a scaffold
/// or the requested drawer does not exist (no content, or its mode disables it).
/// </summary>
public interface IScaffoldFlyoutController
{
    /// <summary>Opens the drawer on the given side (see <see cref="Scaffold.OpenFlyoutAsync"/> for the availability rules).</summary>
    /// <param name="side">The edge the drawer slides in from.</param>
    Task OpenAsync(ScaffoldFlyoutSide side);

    /// <summary>Closes the open drawer, if any; other overlays are unaffected.</summary>
    Task CloseAsync();
}
