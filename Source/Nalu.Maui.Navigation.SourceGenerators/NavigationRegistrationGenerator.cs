using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nalu.Maui.Navigation.SourceGenerators;

/// <summary>
/// Generates trim/AOT-safe Nalu navigation registrations for the compiling assembly:
/// an <c>AddPages()</c> extension on <c>NavigationConfigurator</c> registering every discovered
/// ContentPage (with its inferred page model), and an <c>AddIntents()</c> extension on
/// <c>NavigationRestoreOptions</c> registering every restorable intent discovered through
/// <c>IEnteringAware&lt;T&gt;</c> / <c>IAppearingAware&lt;T&gt;</c> implementations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NavigationRegistrationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Cheap per-compilation gate: only generate for assemblies that consume the library
        // (never for the library itself, where NavigationConfigurator is source-declared).
        var hasNalu = context.CompilationProvider
                             .Select(static (c, _) => c.AssemblyName != "Nalu.Maui.Navigation" && c.GetTypeByMetadataName("Nalu.NavigationConfigurator") is not null)
                             .WithTrackingName("NaluReference");

        var pages = context.SyntaxProvider
                           .CreateSyntaxProvider(
                               static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                               static (ctx, ct) => AnalyzePage(ctx, ct)
                           )
                           .Where(static candidate => candidate is not null)
                           .Select(static (candidate, _) => candidate!)
                           .WithTrackingName("Pages");

        var models = context.SyntaxProvider
                            .CreateSyntaxProvider(
                                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } or InterfaceDeclarationSyntax { BaseList: not null },
                                static (ctx, ct) => AnalyzeModelCandidate(ctx, ct)
                            )
                            .Where(static candidate => candidate is not null)
                            .Select(static (candidate, _) => candidate!)
                            .WithTrackingName("Models");

        // XAML-side discovery: MAUI injects every MauiXaml item as an AdditionalFile. This is
        // the only path that survives the MAUI XAML source generator (its generated x:Class
        // partial — the one carrying the base type — is invisible to other generators).
        var xamlPages = context.AdditionalTextsProvider
                               .Where(static text => text.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                               .Select(static (text, ct) => XamlLeadParser.Parse(text.GetText(ct)?.ToString()))
                               .Where(static lead => lead is not null)
                               .Combine(context.CompilationProvider)
                               .Select(static (pair, ct) => ResolveXamlCandidate(pair.Left!, pair.Right, ct))
                               .Where(static candidate => candidate is not null)
                               .Select(static (candidate, _) => candidate!)
                               .WithTrackingName("XamlPages");

        var input = pages.Collect()
                         .Combine(models.Collect())
                         .Combine(xamlPages.Collect())
                         .Combine(hasNalu)
                         .WithTrackingName("Registrations");

        context.RegisterSourceOutput(input, static (spc, data) => Emit(spc, data.Left.Left.Left, data.Left.Left.Right, data.Left.Right, data.Right));
    }

    #region Symbol analysis

    private static PageCandidate? AnalyzePage(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax) ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(declaration, ct) is not { } symbol ||
            symbol.IsAbstract ||
            symbol.IsGenericType ||
            !DerivesFromContentPage(symbol, out var ancestorFqns) ||
            IsAutoRegistrationDisabled(symbol))
        {
            return null;
        }

        // Partial classes (XAML pages) surface once per base-listed declaration — and only
        // those pass the syntax predicate: the developer's code-behind often declares no base
        // at all (the XamlG-generated part carries it). The FIRST base-listed declaration
        // produces the candidate, deterministically.
        if (!IsCanonicalDeclaration(symbol, declaration, ct))
        {
            return null;
        }

        var (ctorModel, ambiguous) = InferConstructorModel(symbol, ct);

        return new PageCandidate(
            Fqn(symbol),
            symbol.Name,
            ctorModel,
            ambiguous,
            ExtractIntents(symbol),
            ancestorFqns,
            HasAutoNavigationPageAttribute(symbol),
            LocationInfo.From(declaration)
        );
    }

    private static XamlPageCandidate? ResolveXamlCandidate(XamlLead lead, Compilation compilation, CancellationToken ct)
    {
        if (compilation.GetTypeByMetadataName(lead.ClassMetadataName) is not { } symbol ||
            symbol.IsAbstract ||
            symbol.IsGenericType ||
            IsAutoRegistrationDisabled(symbol))
        {
            return null;
        }

        var rootType = ResolveRootType(lead, compilation);
        var rootIsPage = rootType is not null && (IsContentPageSymbol(rootType) || DerivesFromContentPage(rootType, out _));

        // The candidate's ancestors are the root type and ITS chain: they feed the
        // base-page exclusion exactly like syntax-discovered candidates.
        var ancestors = new List<string>();
        var rootFqn = rootType is not null
            ? Fqn(rootType)
            : "global::" + (lead.RootClrNamespace is { } clrNs ? clrNs + "." + lead.RootName : lead.RootName);

        if (rootType is not null && !IsContentPageSymbol(rootType))
        {
            ancestors.Add(rootFqn);

            if (DerivesFromContentPage(rootType, out var rootAncestors))
            {
                foreach (var ancestor in rootAncestors)
                {
                    ancestors.Add(ancestor);
                }
            }
        }

        var (ctorModel, ambiguous) = InferConstructorModel(symbol, ct);

        var candidate = new PageCandidate(
            Fqn(symbol),
            symbol.Name,
            ctorModel,
            ambiguous,
            ExtractIntents(symbol),
            ancestors.Count == 0 ? EquatableArray<string>.Empty : new EquatableArray<string>([.. ancestors]),
            HasAutoNavigationPageAttribute(symbol),
            default
        );

        return new XamlPageCandidate(candidate, rootFqn, rootIsPage);
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

        // Fallback for the default MAUI namespace when no XmlnsDefinition is visible
        // (e.g. reference assemblies without attributes).
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

    private static bool IsContentPageSymbol(INamedTypeSymbol type)
        => type is { Name: "ContentPage", ContainingNamespace: { Name: "Controls", ContainingNamespace: { Name: "Maui", ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true } } } };

    private static (ModelRef? Model, bool Ambiguous) InferConstructorModel(INamedTypeSymbol page, CancellationToken ct)
    {
        // Primary signal: a constructor parameter assigned to BindingContext in a ctor body.
        var assignedTypes = new List<ITypeSymbol>();

        foreach (var ctor in page.InstanceConstructors)
        {
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

                    if (parameter is not null && !assignedTypes.Contains(parameter.Type, SymbolEqualityComparer.Default))
                    {
                        assignedTypes.Add(parameter.Type);
                    }
                }
            }
        }

        if (assignedTypes.Count > 1)
        {
            return (null, true);
        }

        if (assignedTypes.Count == 1)
        {
            return (CreateModelRef(assignedTypes[0]), false);
        }

        // Fallback: exactly one distinct INotifyPropertyChanged constructor parameter type.
        var inpcParameterTypes = page.InstanceConstructors
                                     .Where(static c => c.DeclaredAccessibility == Accessibility.Public)
                                     .SelectMany(static c => c.Parameters)
                                     .Select(static p => p.Type)
                                     .Where(static t => ImplementsInpc(t))
                                     .Distinct(SymbolEqualityComparer.Default)
                                     .OfType<ITypeSymbol>()
                                     .ToList();

        return inpcParameterTypes.Count == 1
            ? (CreateModelRef(inpcParameterTypes[0]), false)
            : (null, false);
    }

    private static bool IsBindingContextTarget(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax { Identifier.Text: "BindingContext" }
           || expression is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "BindingContext" };

    private static ModelRef? CreateModelRef(ITypeSymbol type)
        => type is INamedTypeSymbol named && !named.IsGenericType
            ? new ModelRef(Fqn(named), named.TypeKind == TypeKind.Interface, ImplementsInpc(named), ExtractIntents(named))
            : null;

    private static ModelCandidate? AnalyzeModelCandidate(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var declaration = (TypeDeclarationSyntax) ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(declaration, ct) is not { } symbol ||
            symbol.IsGenericType ||
            !ImplementsInpc(symbol))
        {
            return null;
        }

        if (!IsCanonicalDeclaration(symbol, declaration, ct))
        {
            return null;
        }

        var isInterface = symbol.TypeKind == TypeKind.Interface;

        var inpcInterfaces = isInterface
            ? EquatableArray<string>.Empty
            : new EquatableArray<string>(
                symbol.AllInterfaces
                      .Where(static i => !IsInpcInterface(i) && ImplementsInpc(i) && !i.IsGenericType)
                      .Select(Fqn)
                      .ToArray()
            );

        return new ModelCandidate(Fqn(symbol), symbol.Name, isInterface, symbol.IsAbstract, inpcInterfaces, ExtractIntents(symbol));
    }

    private static EquatableArray<IntentSpec> ExtractIntents(INamedTypeSymbol type)
    {
        List<IntentSpec>? intents = null;

        foreach (var iface in type.AllInterfaces)
        {
            if (!IsNaluType(iface, "IEnteringAware`1") && !IsNaluType(iface, "IAppearingAware`1"))
            {
                continue;
            }

            if (iface.TypeArguments[0] is not INamedTypeSymbol intentType || intentType.IsGenericType)
            {
                continue;
            }

            var spec = CreateIntentSpec(intentType);

            intents ??= [];

            if (!intents.Contains(spec))
            {
                intents.Add(spec);
            }
        }

        return intents is null ? EquatableArray<IntentSpec>.Empty : new EquatableArray<IntentSpec>(intents.ToArray());
    }

    private static IntentSpec CreateIntentSpec(INamedTypeSymbol intentType)
    {
        var enabled = true;
        string? explicitId = null;

        foreach (var attribute in intentType.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass || !IsNaluType(attributeClass, "AutoNavigationIntentAttribute"))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string typeId)
            {
                explicitId = typeId;
            }

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument is { Key: "Enabled", Value.Value: bool value })
                {
                    enabled = value;
                }
            }
        }

        var isAwaitable = intentType.AllInterfaces.Any(static i => IsNaluType(i, "IAwaitableIntentController"));

        return new IntentSpec(Fqn(intentType), intentType.Name, explicitId, enabled, isAwaitable);
    }

    private static string Fqn(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// The canonical declaration of a (possibly partial) type is the FIRST one carrying a
    /// base list — the only kind the syntax predicates visit. Emitting the candidate from it
    /// keeps discovery deterministic and independent of which partial declares the base.
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

    private static bool DerivesFromContentPage(INamedTypeSymbol type, out EquatableArray<string> ancestorFqns)
    {
        List<string>? ancestors = null;

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType is { Name: "ContentPage", ContainingNamespace: { Name: "Controls", ContainingNamespace: { Name: "Maui", ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true } } } })
            {
                ancestorFqns = ancestors is null ? EquatableArray<string>.Empty : new EquatableArray<string>([.. ancestors]);

                return true;
            }

            (ancestors ??= []).Add(Fqn(baseType));
        }

        ancestorFqns = EquatableArray<string>.Empty;

        return false;
    }

    private static bool HasAutoNavigationPageAttribute(INamedTypeSymbol type)
        => type.GetAttributes().Any(static a => a.AttributeClass is { } attributeClass && IsNaluType(attributeClass, "AutoNavigationPageAttribute"));

    private static bool IsAutoRegistrationDisabled(INamedTypeSymbol type)
        => type.GetAttributes()
               .Any(
                   static a => a.AttributeClass is { } attributeClass
                               && IsNaluType(attributeClass, "AutoNavigationPageAttribute")
                               && a.NamedArguments.Any(static n => n is { Key: "Enabled", Value.Value: false })
               );

    private static bool IsNaluType(INamedTypeSymbol type, string metadataName)
        => type.MetadataName == metadataName && type.ContainingNamespace is { Name: "Nalu", ContainingNamespace.IsGlobalNamespace: true };

    private static bool IsInpcInterface(ITypeSymbol type)
        => type is { Name: "INotifyPropertyChanged", ContainingNamespace: { Name: "ComponentModel", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } };

    private static bool ImplementsInpc(ITypeSymbol type)
        => IsInpcInterface(type) || type.AllInterfaces.Any(static i => IsInpcInterface(i));

    #endregion

    #region Registration resolution & emission

    private enum RegistrationKind
    {
        ViewOnly,
        Model,
        InterfaceModel
    }

    private sealed record Registration(RegistrationKind Kind, string PageFqn, string? ModelFqn, string? ImplementationFqn);

    private static void Emit(SourceProductionContext context, ImmutableArray<PageCandidate> pages, ImmutableArray<ModelCandidate> models, ImmutableArray<XamlPageCandidate> xamlPages, bool hasNalu)
    {
        if (!hasNalu)
        {
            return;
        }

        // Merge the two discovery paths. Syntax candidates come first so the dedupe below
        // keeps their (located) diagnostics; XAML candidates add what only the .xaml files
        // reveal — pages whose base type lives in a generated partial we cannot see.
        var merged = pages.ToList();
        var knownPageFqns = new HashSet<string>(merged.Select(static p => p.PageFqn), StringComparer.Ordinal);

        foreach (var xamlPage in xamlPages.Where(static x => x.RootIsPage))
        {
            merged.Add(xamlPage.Candidate);
            knownPageFqns.Add(xamlPage.Candidate.PageFqn);
        }

        // Fixpoint for XAML base-page chains: a page whose root did not resolve to a
        // ContentPage symbol is still a page when its root IS another discovered page
        // (a base page itself defined in XAML, whose generated partial is invisible).
        var deferred = xamlPages.Where(static x => !x.RootIsPage).ToList();
        var progressed = true;

        while (progressed)
        {
            progressed = false;

            for (var i = deferred.Count - 1; i >= 0; i--)
            {
                if (knownPageFqns.Contains(deferred[i].RootFqn))
                {
                    merged.Add(deferred[i].Candidate);
                    knownPageFqns.Add(deferred[i].Candidate.PageFqn);
                    deferred.RemoveAt(i);
                    progressed = true;
                }
            }
        }

        // Defensive dedupe (partial declarations are already collapsed at analysis time).
        var pageList = merged
                       .GroupBy(static p => p.PageFqn, StringComparer.Ordinal)
                       .Select(static g => g.First())
                       .OrderBy(static p => p.PageFqn, StringComparer.Ordinal)
                       .ToList();

        // A concrete page other candidates derive from (an app-level ContentPageBase) is
        // infrastructure, not a navigation destination: excluded unless [AutoNavigationPage]
        // opts it back in explicitly.
        var baseFqns = new HashSet<string>(pageList.SelectMany(static p => p.AncestorFqns), StringComparer.Ordinal);
        pageList = pageList.Where(p => p.ExplicitlyEnabled || !baseFqns.Contains(p.PageFqn)).ToList();

        var modelList = models
                        .GroupBy(static m => m.Fqn, StringComparer.Ordinal)
                        .Select(static g => g.First())
                        .ToList();

        var classesByName = modelList
                            .Where(static m => m is { IsInterface: false, IsAbstract: false })
                            .ToLookup(static m => m.Name, StringComparer.Ordinal);

        var interfacesByName = modelList
                               .Where(static m => m.IsInterface)
                               .ToLookup(static m => m.Name, StringComparer.Ordinal);

        var candidatesByFqn = modelList.ToDictionary(static m => m.Fqn, StringComparer.Ordinal);

        var implementationsByInterfaceFqn = modelList
                                            .Where(static m => m is { IsInterface: false, IsAbstract: false })
                                            .SelectMany(static m => m.InpcInterfaceFqns.Select(i => (Interface: i, Implementation: m)))
                                            .ToLookup(static pair => pair.Interface, static pair => pair.Implementation, StringComparer.Ordinal);

        var registrations = new List<Registration>();
        var intents = new List<IntentSpec>();

        void CollectIntents(EquatableArray<IntentSpec> specs)
        {
            foreach (var spec in specs)
            {
                if (!intents.Contains(spec))
                {
                    intents.Add(spec);
                }
            }
        }

        foreach (var page in pageList)
        {
            if (page.AmbiguousCtorModels)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.AmbiguousModel, page.Location.ToLocation(), page.PageName));

                continue;
            }

            if (page.CtorModel is { } model)
            {
                if (!model.ImplementsInpc)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ModelNotInpc, page.Location.ToLocation(), model.Fqn, page.PageName));

                    continue;
                }

                if (model.IsInterface)
                {
                    var implementations = implementationsByInterfaceFqn[model.Fqn].ToList();

                    if (implementations.Count != 1)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(Diagnostics.UnresolvedInterfaceModel, page.Location.ToLocation(), model.Fqn, page.PageName, implementations.Count)
                        );

                        continue;
                    }

                    registrations.Add(new Registration(RegistrationKind.InterfaceModel, page.PageFqn, model.Fqn, implementations[0].Fqn));
                    CollectIntents(page.PageIntents);
                    CollectIntents(model.Intents);
                    CollectIntents(implementations[0].Intents);
                }
                else
                {
                    registrations.Add(new Registration(RegistrationKind.Model, page.PageFqn, model.Fqn, null));
                    CollectIntents(page.PageIntents);
                    CollectIntents(model.Intents);
                }

                continue;
            }

            // Naming-convention fallback: MyPage -> MyPageModel (via IMyPageModel when implemented).
            var conventionName = page.PageName + "Model";
            var conventionClasses = classesByName[conventionName].ToList();

            if (conventionClasses.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.AmbiguousConventionModel, page.Location.ToLocation(), conventionName, page.PageName));
                registrations.Add(new Registration(RegistrationKind.ViewOnly, page.PageFqn, null, null));
                CollectIntents(page.PageIntents);

                continue;
            }

            if (conventionClasses.Count == 1)
            {
                var implementation = conventionClasses[0];

                var conventionInterface = interfacesByName["I" + conventionName]
                    .FirstOrDefault(i => implementation.InpcInterfaceFqns.Contains(i.Fqn, StringComparer.Ordinal));

                if (conventionInterface is not null)
                {
                    registrations.Add(new Registration(RegistrationKind.InterfaceModel, page.PageFqn, conventionInterface.Fqn, implementation.Fqn));
                    CollectIntents(conventionInterface.Intents);
                }
                else
                {
                    registrations.Add(new Registration(RegistrationKind.Model, page.PageFqn, implementation.Fqn, null));
                }

                CollectIntents(page.PageIntents);
                CollectIntents(implementation.Intents);

                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ViewOnlyPage, page.Location.ToLocation(), page.PageName));
            registrations.Add(new Registration(RegistrationKind.ViewOnly, page.PageFqn, null, null));
            CollectIntents(page.PageIntents);
        }

        // Interface models resolved through a ctor parameter carry no implementation intents
        // when the implementation is source-declared but only referenced by interface: merge them.
        foreach (var registration in registrations)
        {
            if (registration.ImplementationFqn is { } implementationFqn && candidatesByFqn.TryGetValue(implementationFqn, out var candidate))
            {
                CollectIntents(candidate.Intents);
            }
        }

        var restorableIntents = intents
                                .Where(static i => i is { Restorable: true, IsAwaitable: false })
                                .GroupBy(static i => i.Fqn, StringComparer.Ordinal)
                                .Select(static g => g.First())
                                .OrderBy(static i => i.TypeId, StringComparer.Ordinal)
                                .ThenBy(static i => i.Fqn, StringComparer.Ordinal)
                                .ToList();

        foreach (var collision in restorableIntents.GroupBy(static i => i.TypeId, StringComparer.Ordinal).Where(static g => g.Count() > 1))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Diagnostics.IntentIdCollision, Location.None, string.Join(", ", collision.Select(static i => i.Fqn)), collision.Key)
            );
        }

        context.AddSource("NaluNavigationRegistrations.g.cs", SourceText.From(GenerateSource(registrations, restorableIntents), Encoding.UTF8));
    }

    private static string GenerateSource(List<Registration> registrations, List<IntentSpec> restorableIntents)
    {
        var builder = new StringBuilder(
            """
            // <auto-generated/>
            // Generated by Nalu.Maui.Navigation.SourceGenerators: trim/AOT-safe navigation registrations.
            #nullable enable

            // Global namespace on purpose: the extensions are in scope from any namespace, without usings.
            /// <summary>Generated Nalu navigation registrations for this assembly.</summary>
            [global::System.CodeDom.Compiler.GeneratedCode("Nalu.Maui.Navigation.SourceGenerators", "1.0")]
            internal static class NaluNavigationRegistrations
            {
                /// <summary>
                /// Registers every non-excluded <c>ContentPage</c> discovered in this assembly with its
                /// inferred page model (constructor <c>BindingContext</c> assignment, single
                /// <c>INotifyPropertyChanged</c> constructor parameter, or <c>MyPage -&gt; MyPageModel</c>
                /// naming convention), falling back to a view-only registration.
                /// </summary>
                public static global::Nalu.NavigationConfigurator AddPages(this global::Nalu.NavigationConfigurator navigation)
                {

            """
        );

        if (registrations.Count == 0)
        {
            builder.AppendLine("        // No pages were discovered in THIS assembly. AddPages() scans only the compiling");
            builder.AppendLine("        // project: pages living in other assemblies must call their own generated");
            builder.AppendLine("        // AddPages() there or be registered manually via AddPage<...>(). A page is any");
            builder.AppendLine("        // non-abstract, non-generic class deriving (directly or indirectly) from");
            builder.AppendLine("        // Microsoft.Maui.Controls.ContentPage, in any partial declaration.");
        }

        foreach (var registration in registrations)
        {
            builder.Append("        ");

            builder.AppendLine(
                registration.Kind switch
                {
                    RegistrationKind.ViewOnly => $"navigation.AddPage<{registration.PageFqn}>();",
                    RegistrationKind.Model => $"navigation.AddPage<{registration.ModelFqn}, {registration.PageFqn}>();",
                    _ => $"navigation.AddPage<{registration.ModelFqn}, {registration.ImplementationFqn}, {registration.PageFqn}>();"
                }
            );
        }

        builder.Append(
            """

                    return navigation;
                }

                /// <summary>
                /// Registers every restorable intent discovered in this assembly through
                /// <c>IEnteringAware&lt;T&gt;</c> / <c>IAppearingAware&lt;T&gt;</c> implementations,
                /// honoring <c>[AutoNavigationIntent]</c>.
                /// </summary>
                public static global::Nalu.NavigationRestoreOptions AddIntents(this global::Nalu.NavigationRestoreOptions options)
                {

            """
        );

        foreach (var intent in restorableIntents)
        {
            builder.Append("        ").AppendLine($"options.AddIntent<{intent.Fqn}>(\"{intent.TypeId}\");");
        }

        builder.Append(
            """

                    return options;
                }
            }

            """
        );

        return builder.ToString();
    }

    #endregion
}
