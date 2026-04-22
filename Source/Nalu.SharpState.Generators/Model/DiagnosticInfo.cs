using Microsoft.CodeAnalysis;

namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Value-equatable diagnostic descriptor + location + arguments that can be carried through the incremental
/// pipeline and replayed via <see cref="SourceProductionContext.ReportDiagnostic"/>.
/// </summary>
internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic()
    {
        var location = Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None;
        var args = new object?[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(Descriptor, location, args);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] args)
        => new(descriptor, LocationInfo.FromLocation(location), new EquatableArray<string>(args));
}
