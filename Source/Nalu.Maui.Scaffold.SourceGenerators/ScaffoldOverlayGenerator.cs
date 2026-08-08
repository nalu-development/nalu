using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nalu.Maui.Navigation.SourceGenerators;

namespace Nalu.Maui.Scaffold.SourceGenerators;

/// <summary>
/// Generates trim/AOT-safe scaffold overlay registrations for the compiling assembly: an
/// <c>AddOverlays()</c> extension on <c>IScaffoldConfigurator</c>. Overlays are anchored on
/// classes whose public constructor takes <c>Nalu.IOverlayRef</c> (or classes marked
/// <c>[AutoOverlay]</c>): a <c>View</c>-derived anchor registers view-only, any other anchor
/// is a model paired to the View whose constructor takes it (BindingContext assignment
/// preferred), the <c>FooModel → FooView</c> naming convention, or an explicit
/// <c>[AutoOverlay(typeof(...))]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ScaffoldOverlayGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hasNalu = context.CompilationProvider
                             .Select(static (c, _) => c.AssemblyName != "Nalu.Maui.Scaffold" && c.GetTypeByMetadataName("Nalu.IScaffoldConfigurator") is not null)
                             .WithTrackingName("NaluScaffoldReference");

        var anchors = context.SyntaxProvider
                             .CreateSyntaxProvider(
                                 static (node, _) => IsAnchorCandidate(node),
                                 static (ctx, ct) => AnalyzeAnchor(ctx, ct)
                             )
                             .Where(static candidate => candidate is not null)
                             .Select(static (candidate, _) => candidate!)
                             .WithTrackingName("OverlayAnchors");

        var views = context.SyntaxProvider
                           .CreateSyntaxProvider(
                               static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                               static (ctx, ct) => AnalyzeViewCandidate(ctx, ct)
                           )
                           .Where(static candidate => candidate is not null)
                           .Select(static (candidate, _) => candidate!)
                           .WithTrackingName("OverlayViews");

        var input = anchors.Collect()
                           .Combine(views.Collect())
                           .Combine(hasNalu)
                           .WithTrackingName("OverlayRegistrations");

        context.RegisterSourceOutput(input, static (spc, data) => Emit(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    #region Symbol analysis

    private static bool IsAnchorCandidate(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax declaration)
        {
            return false;
        }

        // [AutoOverlay] may hide behind aliases: any attributed class goes to the (cheap) transform.
        if (declaration.AttributeLists.Count > 0)
        {
            return true;
        }

        if (HasOverlayRefParameter(declaration.ParameterList))
        {
            return true;
        }

        foreach (var member in declaration.Members)
        {
            if (member is ConstructorDeclarationSyntax ctor && HasOverlayRefParameter(ctor.ParameterList))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOverlayRefParameter(ParameterListSyntax? parameterList)
    {
        if (parameterList is null)
        {
            return false;
        }

        foreach (var parameter in parameterList.Parameters)
        {
            var name = parameter.Type switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right.Identifier.ValueText,
                _ => null
            };

            if (name == "IOverlayRef")
            {
                return true;
            }
        }

        return false;
    }

    private static OverlayAnchor? AnalyzeAnchor(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax) ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(declaration, ct) is not { } symbol || symbol.IsAbstract || symbol.IsGenericType)
        {
            return null;
        }

        if (!ReferenceEquals(symbol.DeclaringSyntaxReferences[0].GetSyntax(ct), declaration))
        {
            return null;
        }

        var enabled = true;
        var hasAttribute = false;
        string? explicitViewFqn = null;
        var hasInvalidExplicitView = false;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass || !IsNaluType(attributeClass, "AutoOverlayAttribute"))
            {
                continue;
            }

            hasAttribute = true;

            if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is INamedTypeSymbol viewType)
            {
                if (viewType is { IsAbstract: false, IsGenericType: false } && DerivesFromView(viewType))
                {
                    explicitViewFqn = Fqn(viewType);
                }
                else
                {
                    hasInvalidExplicitView = true;
                }
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument is { Key: "Enabled", Value.Value: bool value })
                {
                    enabled = value;
                }
            }
        }

        if (!enabled)
        {
            return null;
        }

        if (!hasAttribute && !HasOverlayRefConstructorParameter(symbol))
        {
            return null;
        }

        return new OverlayAnchor(
            Fqn(symbol),
            symbol.Name,
            DerivesFromView(symbol),
            explicitViewFqn,
            hasInvalidExplicitView,
            LocationInfo.From(declaration)
        );
    }

    private static bool HasOverlayRefConstructorParameter(INamedTypeSymbol type)
        => type.InstanceConstructors.Any(
            static ctor => ctor.DeclaredAccessibility == Accessibility.Public
                           && ctor.Parameters.Any(static p => p.Type is INamedTypeSymbol named && IsNaluType(named, "IOverlayRef"))
        );

    private static OverlayViewCandidate? AnalyzeViewCandidate(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax) ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(declaration, ct) is not { } symbol ||
            symbol.IsAbstract ||
            symbol.IsGenericType ||
            !DerivesFromView(symbol))
        {
            return null;
        }

        if (!ReferenceEquals(symbol.DeclaringSyntaxReferences[0].GetSyntax(ct), declaration))
        {
            return null;
        }

        var parameterTypes = new List<string>();
        var bindingContextTypes = new List<string>();

        foreach (var ctor in symbol.InstanceConstructors.Where(static c => c.DeclaredAccessibility == Accessibility.Public))
        {
            foreach (var parameter in ctor.Parameters)
            {
                var fqn = Fqn(parameter.Type);

                if (!parameterTypes.Contains(fqn))
                {
                    parameterTypes.Add(fqn);
                }
            }

            foreach (var reference in ctor.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(ct) is not ConstructorDeclarationSyntax ctorSyntax)
                {
                    continue;
                }

                foreach (var assignment in ctorSyntax.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (!IsBindingContextTarget(assignment.Left) || assignment.Right is not IdentifierNameSyntax identifier)
                    {
                        continue;
                    }

                    var parameter = ctor.Parameters.FirstOrDefault(p => p.Name == identifier.Identifier.Text);

                    if (parameter is not null)
                    {
                        var fqn = Fqn(parameter.Type);

                        if (!bindingContextTypes.Contains(fqn))
                        {
                            bindingContextTypes.Add(fqn);
                        }
                    }
                }
            }
        }

        return new OverlayViewCandidate(
            Fqn(symbol),
            symbol.Name,
            new EquatableArray<string>(parameterTypes.ToArray()),
            new EquatableArray<string>(bindingContextTypes.ToArray())
        );
    }

    private static bool IsBindingContextTarget(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax { Identifier.Text: "BindingContext" }
           || expression is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "BindingContext" };

    private static string Fqn(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static bool IsNaluType(INamedTypeSymbol type, string metadataName)
        => type.MetadataName == metadataName && type.ContainingNamespace is { Name: "Nalu", ContainingNamespace.IsGlobalNamespace: true };

    private static bool DerivesFromView(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType is { Name: "View", ContainingNamespace: { Name: "Controls", ContainingNamespace: { Name: "Maui", ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true } } } })
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Registration resolution & emission

    private sealed record Registration(string? ModelFqn, string ViewFqn);

    private static void Emit(SourceProductionContext context, ImmutableArray<OverlayAnchor> anchors, ImmutableArray<OverlayViewCandidate> views, bool hasNalu)
    {
        if (!hasNalu)
        {
            return;
        }

        var anchorList = anchors
                         .GroupBy(static a => a.Fqn, StringComparer.Ordinal)
                         .Select(static g => g.First())
                         .OrderBy(static a => a.Fqn, StringComparer.Ordinal)
                         .ToList();

        var viewList = views
                       .GroupBy(static v => v.Fqn, StringComparer.Ordinal)
                       .Select(static g => g.First())
                       .ToList();

        var viewsByName = viewList.ToLookup(static v => v.Name, StringComparer.Ordinal);
        var registrations = new List<Registration>();

        foreach (var anchor in anchorList)
        {
            if (anchor.HasInvalidExplicitView)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidExplicitView, anchor.Location.ToLocation(), anchor.Name));

                continue;
            }

            if (anchor.IsView)
            {
                registrations.Add(new Registration(null, anchor.Fqn));

                continue;
            }

            if (anchor.ExplicitViewFqn is { } explicitView)
            {
                registrations.Add(new Registration(anchor.Fqn, explicitView));

                continue;
            }

            // Views whose constructor takes the model; BindingContext assignment wins ties.
            var matches = viewList.Where(v => v.CtorParamTypeFqns.Contains(anchor.Fqn, StringComparer.Ordinal)).ToList();

            if (matches.Count > 1)
            {
                var assigning = matches.Where(v => v.BindingContextParamTypeFqns.Contains(anchor.Fqn, StringComparer.Ordinal)).ToList();

                if (assigning.Count == 1)
                {
                    matches = assigning;
                }
            }

            if (matches.Count == 1)
            {
                registrations.Add(new Registration(anchor.Fqn, matches[0].Fqn));

                continue;
            }

            if (matches.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.AmbiguousViewForModel, anchor.Location.ToLocation(), anchor.Name));

                continue;
            }

            // Naming convention: FooModel -> FooView (or Foo).
            var baseName = anchor.Name.EndsWith("Model", StringComparison.Ordinal)
                ? anchor.Name.Substring(0, anchor.Name.Length - "Model".Length)
                : anchor.Name;

            var conventionMatches = viewsByName[baseName + "View"].Concat(viewsByName[baseName]).ToList();

            if (conventionMatches.Count == 1)
            {
                registrations.Add(new Registration(anchor.Fqn, conventionMatches[0].Fqn));

                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoViewForModel, anchor.Location.ToLocation(), anchor.Name, baseName));
        }

        context.AddSource("NaluScaffoldRegistrations.g.cs", SourceText.From(GenerateSource(registrations), Encoding.UTF8));
    }

    private static string GenerateSource(List<Registration> registrations)
    {
        var builder = new StringBuilder(
            """
            // <auto-generated/>
            // Generated by Nalu.Maui.Scaffold.SourceGenerators: trim/AOT-safe overlay registrations.
            #nullable enable

            // Global namespace on purpose: the extension is in scope from any namespace, without usings.
            /// <summary>Generated Nalu scaffold overlay registrations for this assembly.</summary>
            [global::System.CodeDom.Compiler.GeneratedCode("Nalu.Maui.Scaffold.SourceGenerators", "1.0")]
            internal static class NaluScaffoldRegistrations
            {
                /// <summary>
                /// Registers every overlay discovered in this assembly: classes taking
                /// <c>IOverlayRef</c> in their constructor (or marked <c>[AutoOverlay]</c>) —
                /// <c>View</c>-derived ones as view-only overlays, others as models paired with
                /// their resolved view.
                /// </summary>
                public static global::Nalu.IScaffoldConfigurator AddOverlays(this global::Nalu.IScaffoldConfigurator scaffold)
                {

            """
        );

        foreach (var registration in registrations)
        {
            builder.Append("        ");

            builder.AppendLine(
                registration.ModelFqn is null
                    ? $"scaffold.AddOverlay<{registration.ViewFqn}>();"
                    : $"scaffold.AddOverlay<{registration.ModelFqn}, {registration.ViewFqn}>();"
            );
        }

        builder.Append(
            """

                    return scaffold;
                }
            }

            """
        );

        return builder.ToString();
    }

    #endregion
}
