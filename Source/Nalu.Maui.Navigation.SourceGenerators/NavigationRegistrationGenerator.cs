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

        var input = pages.Collect()
                         .Combine(models.Collect())
                         .Combine(hasNalu)
                         .WithTrackingName("Registrations");

        context.RegisterSourceOutput(input, static (spc, data) => Emit(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    #region Symbol analysis

    private static PageCandidate? AnalyzePage(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax) ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(declaration, ct) is not { } symbol ||
            symbol.IsAbstract ||
            symbol.IsGenericType ||
            !DerivesFromContentPage(symbol) ||
            IsAutoRegistrationDisabled(symbol))
        {
            return null;
        }

        // Partial classes (XAML pages) surface once per declaration: only the first
        // declaration produces the candidate, deterministically.
        if (!ReferenceEquals(symbol.DeclaringSyntaxReferences[0].GetSyntax(ct), declaration))
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
            LocationInfo.From(declaration)
        );
    }

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

        if (!ReferenceEquals(symbol.DeclaringSyntaxReferences[0].GetSyntax(ct), declaration))
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

    private static bool DerivesFromContentPage(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType is { Name: "ContentPage", ContainingNamespace: { Name: "Controls", ContainingNamespace: { Name: "Maui", ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true } } } })
            {
                return true;
            }
        }

        return false;
    }

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

    private static void Emit(SourceProductionContext context, ImmutableArray<PageCandidate> pages, ImmutableArray<ModelCandidate> models, bool hasNalu)
    {
        if (!hasNalu)
        {
            return;
        }

        // Defensive dedupe (partial declarations are already collapsed at analysis time).
        var pageList = pages
                       .GroupBy(static p => p.PageFqn, StringComparer.Ordinal)
                       .Select(static g => g.First())
                       .OrderBy(static p => p.PageFqn, StringComparer.Ordinal)
                       .ToList();

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
