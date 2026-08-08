namespace Nalu;

/// <summary>
/// Tunes how the Nalu scaffold source generator registers an overlay (the generated
/// <c>AddOverlays()</c>).
/// </summary>
/// <remarks>
/// Without the attribute, any class whose public constructor takes <see cref="IOverlayRef"/>
/// is discovered automatically: a <see cref="View"/> subclass registers as a VIEW-ONLY overlay
/// (<c>AddOverlay&lt;TView&gt;()</c>); any other class is an overlay MODEL whose view is
/// resolved from a <see cref="View"/> constructor taking the model type (or the
/// <c>FooModel → FooView</c> naming convention). The attribute opts IN a class the anchor
/// misses, opts OUT a false positive (<see cref="Enabled"/>), or names the view explicitly.
/// </remarks>
/// <param name="viewType">
/// Optional explicit view type for a model — overrides constructor/naming-convention view
/// resolution (useful when several views take the same model).
/// </param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoOverlayAttribute(Type? viewType = null) : Attribute
{
    /// <summary>Gets the explicit view type; null resolves the view automatically.</summary>
    public Type? ViewType { get; } = viewType;

    /// <summary>
    /// Whether the automatic registration is enabled for this class. Defaults to true.
    /// When false the class is skipped by the generated <c>AddOverlays()</c>; it can still be
    /// registered manually via <see cref="IScaffoldConfigurator.AddOverlay{TModel,TView}()"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
