using Nalu.Maui.Navigation.SourceGenerators;

namespace Nalu.Maui.Scaffold.SourceGenerators;

/// <summary>
/// An overlay anchor: a class opted in by an <c>IOverlayRef</c> constructor parameter or an
/// <c>[AutoOverlay]</c> attribute. A <c>View</c>-derived anchor is a VIEW-ONLY overlay; any
/// other anchor is an overlay MODEL whose view is resolved against the view candidates.
/// </summary>
/// <param name="ExplicitViewFqn">[AutoOverlay(typeof(...))] view, already validated as a View.</param>
/// <param name="HasInvalidExplicitView">[AutoOverlay(typeof(...))] named a non-View type.</param>
internal sealed record OverlayAnchor(
    string Fqn,
    string Name,
    bool IsView,
    string? ExplicitViewFqn,
    bool HasInvalidExplicitView,
    LocationInfo Location
);

/// <summary>
/// A source-declared, non-abstract <c>View</c> subclass — the search space for resolving an
/// overlay model's view.
/// </summary>
/// <param name="CtorParamTypeFqns">Types of all public constructor parameters.</param>
/// <param name="BindingContextParamTypeFqns">Constructor parameter types assigned to BindingContext.</param>
internal sealed record OverlayViewCandidate(
    string Fqn,
    string Name,
    EquatableArray<string> CtorParamTypeFqns,
    EquatableArray<string> BindingContextParamTypeFqns
);
