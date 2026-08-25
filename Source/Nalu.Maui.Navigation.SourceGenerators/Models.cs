namespace Nalu.Maui.Navigation.SourceGenerators;

/// <summary>An intent type discovered through IEnteringAware&lt;T&gt; / IAppearingAware&lt;T&gt;.</summary>
/// <param name="Fqn">Fully-qualified (global::) type name.</param>
/// <param name="ShortName">The type's short name — the default restore type id.</param>
/// <param name="ExplicitId">[AutoNavigationIntent("type-id")] when present.</param>
/// <param name="Restorable">[AutoNavigationIntent(Enabled = ...)]; true by default.</param>
/// <param name="IsAwaitable">Implements IAwaitableIntentController: never restorable.</param>
internal sealed record IntentSpec(string Fqn, string ShortName, string? ExplicitId, bool Restorable, bool IsAwaitable)
{
    public string TypeId => ExplicitId ?? ShortName;
}

/// <summary>A page-model type referenced by a page constructor.</summary>
internal sealed record ModelRef(string Fqn, bool IsInterface, bool ImplementsInpc, EquatableArray<IntentSpec> Intents);

/// <summary>A discovered navigation destination eligible for automatic registration.</summary>
/// <param name="PageFqn">Fully-qualified (global::) page (or component) type name.</param>
/// <param name="PageName">Short name, input to the naming-convention fallback.</param>
/// <param name="CtorModel">Model inferred from the constructor (BindingContext assignment, else single INPC parameter).</param>
/// <param name="AmbiguousCtorModels">BindingContext is assigned from more than one constructor parameter type.</param>
/// <param name="PageIntents">Intents implemented by the page/component itself (both are their own lifecycle target).</param>
/// <param name="IsComponent">A non-Page class opted in via [AutoNavigationPage] (e.g. a MauiReactor
/// component): registered with the model-less AddPage&lt;T&gt;() overload, no model inference.</param>
internal sealed record PageCandidate(
    string PageFqn,
    string PageName,
    ModelRef? CtorModel,
    bool AmbiguousCtorModels,
    EquatableArray<IntentSpec> PageIntents,
    LocationInfo Location,
    bool IsComponent = false
);

/// <summary>
/// A page discovered through its .xaml file — the path that works even when the x:Class
/// partial (carrying the base type) is emitted by the MAUI XAML source generator, whose
/// output no other generator can see.
/// </summary>
/// <param name="RootFqn">The XAML root type: the page's base. Best-effort FQN when unresolved.</param>
/// <param name="RootIsPage">The root type resolved to a ContentPage(-derived) symbol. When false
/// the candidate participates only via the emit-time fixpoint (XAML base page chains).</param>
internal sealed record XamlPageCandidate(PageCandidate Candidate, string RootFqn, bool RootIsPage);

/// <summary>
/// A source-declared INotifyPropertyChanged class or interface: the search space for the
/// naming-convention fallback and for resolving interface models to their implementation.
/// </summary>
/// <param name="InpcInterfaceFqns">For classes: implemented interfaces that themselves extend INPC.</param>
internal sealed record ModelCandidate(
    string Fqn,
    string Name,
    bool IsInterface,
    bool IsAbstract,
    EquatableArray<string> InpcInterfaceFqns,
    EquatableArray<IntentSpec> Intents
);
