using Nalu.Maui.Navigation.SourceGenerators;

namespace Nalu.Maui.Scaffold.SourceGenerators;

/// <summary>
/// An overlay anchor: a class opted in by an <c>IOverlayRef</c> constructor parameter or an
/// <c>[AutoOverlay]</c> attribute. A <c>View</c>-derived anchor is a VIEW-ONLY overlay; any
/// other anchor is an overlay MODEL whose view is resolved against the view candidates.
/// </summary>
/// <param name="ExplicitViewFqn">[AutoOverlay(typeof(...))] view: concrete and non-generic.</param>
/// <param name="ExplicitViewIsView">The explicit view derives from View at the SYMBOL level. When
/// false it may still be a view whose base is only visible through its .xaml file, so the final
/// verdict is taken at emit time against the discovered view set.</param>
/// <param name="HasInvalidExplicitView">[AutoOverlay(typeof(...))] named an abstract or generic type.</param>
internal sealed record OverlayAnchor(
    string Fqn,
    string Name,
    bool IsView,
    string? ExplicitViewFqn,
    bool ExplicitViewIsView,
    bool HasInvalidExplicitView,
    LocationInfo Location
);

/// <summary>
/// A view discovered through its .xaml file — the path that works even when the x:Class
/// partial (carrying the View base) is emitted by the MAUI XAML source generator, whose
/// output no other generator can see.
/// </summary>
/// <param name="RootFqn">The XAML root type: the view's base. Best-effort FQN when unresolved.</param>
/// <param name="RootIsView">The root type resolved to a View-derived symbol. When false the
/// candidate participates only via the emit-time fixpoint (XAML base-view chains).</param>
internal sealed record XamlOverlayViewCandidate(OverlayViewCandidate Candidate, string RootFqn, bool RootIsView);

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
