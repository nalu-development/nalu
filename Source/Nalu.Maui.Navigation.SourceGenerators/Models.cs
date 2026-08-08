using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nalu.Maui.Navigation.SourceGenerators;

/// <summary>
/// A serializable, equatable stand-in for <see cref="Microsoft.CodeAnalysis.Location"/>:
/// pipeline records must never hold symbols or locations (they root compilations and defeat
/// incremental caching).
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo From(SyntaxNode node)
    {
        var location = node.GetLocation();

        return new LocationInfo(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
    }
}

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

/// <summary>A discovered ContentPage subclass eligible for automatic registration.</summary>
/// <param name="PageFqn">Fully-qualified (global::) page type name.</param>
/// <param name="PageName">Short name, input to the naming-convention fallback.</param>
/// <param name="CtorModel">Model inferred from the constructor (BindingContext assignment, else single INPC parameter).</param>
/// <param name="AmbiguousCtorModels">BindingContext is assigned from more than one constructor parameter type.</param>
/// <param name="PageIntents">Intents implemented by the page itself (view-only pages are their own lifecycle target).</param>
internal sealed record PageCandidate(
    string PageFqn,
    string PageName,
    ModelRef? CtorModel,
    bool AmbiguousCtorModels,
    EquatableArray<IntentSpec> PageIntents,
    LocationInfo Location
);

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
