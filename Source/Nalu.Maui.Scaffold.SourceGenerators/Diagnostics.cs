using Microsoft.CodeAnalysis;

namespace Nalu.Maui.Scaffold.SourceGenerators;

internal static class Diagnostics
{
    private const string _category = "NaluScaffold";

    /// <summary>Warning: an overlay model was discovered but no view could be resolved.</summary>
    public static readonly DiagnosticDescriptor NoViewForModel = new(
        "NALU0101",
        "Overlay model has no resolvable view",
        "No View with a constructor taking '{0}' (nor a '{1}View'/'{1}' naming-convention match) was found; the overlay was skipped — register it manually via AddOverlay or name the view with [AutoOverlay(typeof(...))]",
        _category,
        DiagnosticSeverity.Warning,
        true
    );

    /// <summary>Warning: multiple views match an overlay model.</summary>
    public static readonly DiagnosticDescriptor AmbiguousViewForModel = new(
        "NALU0102",
        "Overlay model matches multiple views",
        "Multiple Views take '{0}' in their constructor; the overlay was skipped — pick one with [AutoOverlay(typeof(...))] or register it manually via AddOverlay",
        _category,
        DiagnosticSeverity.Warning,
        true
    );

    /// <summary>Error: Nalu.Maui.Core's SoftKeyboardManager is enabled in a scaffold-hosted app.</summary>
    public static readonly DiagnosticDescriptor SoftKeyboardManagerUnsupported = new(
        "NALU0104",
        "UseNaluSoftKeyboardManager is not supported with Nalu.Maui.Scaffold",
        "UseNaluSoftKeyboardManager is not supported alongside Nalu.Maui.Scaffold: the scaffold owns the soft-keyboard handling (keyboard-aware bottom sheets and popups; MAUI's iOS keyboard manager is disconnected, Android runs edge-to-edge with adjustResize). Remove the call.",
        _category,
        DiagnosticSeverity.Error,
        true
    );

    /// <summary>Warning: [AutoOverlay(typeof(...))] named a type that is not a source-declared View.</summary>
    public static readonly DiagnosticDescriptor InvalidExplicitView = new(
        "NALU0103",
        "AutoOverlay view type is not a valid overlay view",
        "The view type given to [AutoOverlay] on '{0}' is not a non-abstract, non-generic View; the overlay was skipped",
        _category,
        DiagnosticSeverity.Warning,
        true
    );
}
