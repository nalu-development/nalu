using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nalu.SharpState.Generators.Emit;
using Nalu.SharpState.Generators.Model;

namespace Nalu.SharpState.Generators;

/// <summary>
/// Incremental source generator that emits the state/trigger enums, configurator surface, definition builder,
/// and strongly-typed <c>Instance</c> class for every class annotated with <c>[StateMachineDefinition]</c>.
/// </summary>
[Generator]
public sealed class StateMachineGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var machines = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Nalu.SharpState.StateMachineDefinitionAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) =>
                {
                    if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol
                        || ctx.TargetNode is not ClassDeclarationSyntax classSyntax)
                    {
                        return null;
                    }

                    return StateMachineModel.FromSymbol(classSymbol, classSyntax, ct);
                })
            .Where(static m => m is not null);

        context.RegisterSourceOutput(machines, static (spc, model) => StateMachineEmitter.Emit(spc, model!));
    }
}
