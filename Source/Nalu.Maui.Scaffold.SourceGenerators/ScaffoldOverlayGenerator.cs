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

        // XAML-side discovery: MAUI injects every MauiXaml item as an AdditionalFile. The only
        // path that survives the MAUI XAML source generator (its generated x:Class partial —
        // the one carrying the View base — is invisible to other generators).
        var xamlViews = context.AdditionalTextsProvider
                               .Where(static text => text.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                               .Select(static (text, ct) => XamlLeadParser.Parse(text.GetText(ct)?.ToString()))
                               .Where(static lead => lead is not null)
                               .Combine(context.CompilationProvider)
                               .Select(static (pair, ct) => ResolveXamlViewCandidate(pair.Left!, pair.Right, ct))
                               .Where(static candidate => candidate is not null)
                               .Select(static (candidate, _) => candidate!)
                               .WithTrackingName("XamlOverlayViews");

        var input = anchors.Collect()
                           .Combine(views.Collect())
                           .Combine(xamlViews.Collect())
                           .Combine(hasNalu)
                           .WithTrackingName("OverlayRegistrations");

        context.RegisterSourceOutput(input, static (spc, data) => Emit(spc, data.Left.Left.Left, data.Left.Left.Right, data.Left.Right, data.Right));
    }

    #region Symbol analysis

    /// <summary>The first declaration the anchor predicate visits is the anchor's canonical one.</summary>
    private static bool IsCanonicalAnchorDeclaration(INamedTypeSymbol symbol, ClassDeclarationSyntax declaration, CancellationToken ct)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(ct) is ClassDeclarationSyntax candidate && IsAnchorCandidate(candidate))
            {
                return ReferenceEquals(candidate, declaration);
            }
        }

        return false;
    }

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

        // Partial dedup must mirror THIS provider's predicate: the anchor signal (ctor taking
        // IOverlayRef, or an attribute) may live in a different partial than the base list —
        // e.g. a XAML view-only overlay whose code-behind has the ctor and no base.
        if (!IsCanonicalAnchorDeclaration(symbol, declaration, ct))
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

        if (!IsCanonicalDeclaration(symbol, declaration, ct))
        {
            return null;
        }

        return BuildViewCandidate(symbol, ct);
    }

    /// <summary>Symbol-level view-candidate construction, shared by the syntax and XAML pipelines.</summary>
    private static OverlayViewCandidate BuildViewCandidate(INamedTypeSymbol symbol, CancellationToken ct)
    {
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

    /// <summary>
    /// The canonical declaration of a (possibly partial) type is the FIRST one carrying a
    /// base list — the only kind the syntax predicates visit. Emitting the candidate from it
    /// keeps discovery deterministic and independent of which partial declares the base
    /// (XAML code-behinds often declare none; the XamlG-generated part carries it).
    /// </summary>
    private static bool IsCanonicalDeclaration(INamedTypeSymbol symbol, TypeDeclarationSyntax declaration, CancellationToken ct)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(ct) is TypeDeclarationSyntax { BaseList: not null } candidate)
            {
                return ReferenceEquals(candidate, declaration);
            }
        }

        // No declaration carries a base list (plain classes visited by predicates that do not
        // require one): the first declaration is canonical.
        return ReferenceEquals(symbol.DeclaringSyntaxReferences[0].GetSyntax(ct), declaration);
    }

    private static XamlOverlayViewCandidate? ResolveXamlViewCandidate(XamlLead lead, Compilation compilation, CancellationToken ct)
    {
        if (compilation.GetTypeByMetadataName(lead.ClassMetadataName) is not { } symbol ||
            symbol.IsAbstract ||
            symbol.IsGenericType)
        {
            return null;
        }

        var rootType = ResolveRootType(lead, compilation);
        var rootIsView = rootType is not null && DerivesFromView(rootType);

        var rootFqn = rootType is not null
            ? Fqn(rootType)
            : "global::" + (lead.RootClrNamespace is { } clrNs ? clrNs + "." + lead.RootName : lead.RootName);

        return new XamlOverlayViewCandidate(BuildViewCandidate(symbol, ct), rootFqn, rootIsView);
    }

    private static INamedTypeSymbol? ResolveRootType(XamlLead lead, Compilation compilation)
    {
        if (lead.RootClrNamespace is { } clrNamespace)
        {
            return compilation.GetTypeByMetadataName(clrNamespace + "." + lead.RootName);
        }

        if (lead.RootXmlnsUri is not { } uri)
        {
            return null;
        }

        // Resolve the URI the way XAML does: XmlnsDefinition attributes of the compiling
        // assembly and every referenced assembly map it to CLR namespaces.
        foreach (var assembly in ReferencedAssembliesAndSelf(compilation))
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass is { Name: "XmlnsDefinitionAttribute" } &&
                    attribute.ConstructorArguments.Length >= 2 &&
                    attribute.ConstructorArguments[0].Value is string attributeUri &&
                    attribute.ConstructorArguments[1].Value is string clrNs &&
                    string.Equals(attributeUri, uri, StringComparison.Ordinal) &&
                    compilation.GetTypeByMetadataName(clrNs + "." + lead.RootName) is { } resolved)
                {
                    return resolved;
                }
            }
        }

        // Fallback for the default MAUI namespace when no XmlnsDefinition is visible.
        return uri is "http://schemas.microsoft.com/dotnet/2021/maui" or "http://xamarin.com/schemas/2014/forms"
            ? compilation.GetTypeByMetadataName("Microsoft.Maui.Controls." + lead.RootName)
            : null;
    }

    private static IEnumerable<IAssemblySymbol> ReferencedAssembliesAndSelf(Compilation compilation)
    {
        yield return compilation.Assembly;

        foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            yield return reference;
        }
    }

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

    private static void Emit(SourceProductionContext context, ImmutableArray<OverlayAnchor> anchors, ImmutableArray<OverlayViewCandidate> views, ImmutableArray<XamlOverlayViewCandidate> xamlViews, bool hasNalu)
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

        // Merge the two discovery paths (syntax first: XAML candidates add views whose base
        // type lives in a generated partial we cannot see), with a fixpoint for XAML views
        // based on other XAML views.
        var merged = views.ToList();
        var knownViewFqns = new HashSet<string>(merged.Select(static v => v.Fqn), StringComparer.Ordinal);

        foreach (var xamlView in xamlViews.Where(static x => x.RootIsView))
        {
            merged.Add(xamlView.Candidate);
            knownViewFqns.Add(xamlView.Candidate.Fqn);
        }

        var deferred = xamlViews.Where(static x => !x.RootIsView).ToList();
        var progressed = true;

        while (progressed)
        {
            progressed = false;

            for (var i = deferred.Count - 1; i >= 0; i--)
            {
                if (knownViewFqns.Contains(deferred[i].RootFqn))
                {
                    merged.Add(deferred[i].Candidate);
                    knownViewFqns.Add(deferred[i].Candidate.Fqn);
                    deferred.RemoveAt(i);
                    progressed = true;
                }
            }
        }

        var viewList = merged
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

            // An anchor whose View base is only visible through its .xaml file (invisible
            // generated partial) is still a VIEW-ONLY overlay.
            if (anchor.IsView || knownViewFqns.Contains(anchor.Fqn))
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
