using Microsoft.CodeAnalysis;

namespace Nalu.Maui.Navigation.SourceGenerators;

internal static class Diagnostics
{
    private const string _category = "NaluNavigation";

    /// <summary>Info: a page had no discoverable model and was registered view-only.</summary>
    public static readonly DiagnosticDescriptor ViewOnlyPage = new(
        "NALU0001",
        "Page registered without a page model",
        "Page '{0}' has no discoverable page model and was registered as a view-only page",
        _category,
        DiagnosticSeverity.Info,
        true
    );

    /// <summary>Warning: BindingContext is assigned from multiple constructor parameters.</summary>
    public static readonly DiagnosticDescriptor AmbiguousModel = new(
        "NALU0002",
        "Ambiguous page model",
        "Page '{0}' assigns BindingContext from more than one constructor parameter; the page was skipped — register it manually via AddPage",
        _category,
        DiagnosticSeverity.Warning,
        true
    );

    /// <summary>Error: an interface model has zero or multiple implementations in the assembly.</summary>
    public static readonly DiagnosticDescriptor UnresolvedInterfaceModel = new(
        "NALU0003",
        "Cannot resolve page model implementation",
        "Page model interface '{0}' of page '{1}' has {2} INotifyPropertyChanged implementations in this assembly; the page was skipped — register it manually via AddPage",
        _category,
        DiagnosticSeverity.Warning,
        true
    );

    /// <summary>Warning: the type assigned to BindingContext does not implement INPC.</summary>
    public static readonly DiagnosticDescriptor ModelNotInpc = new(
        "NALU0004",
        "Page model does not implement INotifyPropertyChanged",
        "Type '{0}' is assigned to the BindingContext of page '{1}' but does not implement INotifyPropertyChanged; the page was skipped — register it manually via AddPage",
        _category,
        DiagnosticSeverity.Warning,
        true
    );

    /// <summary>Error: two restorable intents share the same restore type id.</summary>
    public static readonly DiagnosticDescriptor IntentIdCollision = new(
        "NALU0005",
        "Intent restore type id collision",
        "Intent types {0} share the restore type id '{1}'; disambiguate via [AutoNavigationIntent(\"...\")]",
        _category,
        DiagnosticSeverity.Error,
        true
    );

    /// <summary>Warning: the naming convention matched multiple same-named model types.</summary>
    public static readonly DiagnosticDescriptor AmbiguousConventionModel = new(
        "NALU0006",
        "Ambiguous page model by naming convention",
        "Multiple types named '{0}' match page '{1}' by naming convention; the page was registered view-only — register it manually via AddPage to pick one",
        _category,
        DiagnosticSeverity.Warning,
        true
    );
}
