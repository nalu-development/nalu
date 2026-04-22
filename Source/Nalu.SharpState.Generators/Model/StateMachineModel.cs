using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nalu.SharpState.Generators.Diagnostics;

namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Top-level equatable model describing a single <c>[StateMachineDefinition]</c>-annotated class, produced by
/// <see cref="FromSymbol"/> from a syntax+semantic snapshot, cached by the incremental pipeline, and consumed
/// by the emitter.
/// </summary>
internal sealed record StateMachineModel(
    string Hintname,
    string Namespace,
    string ClassName,
    string ClassKeyword,
    string ClassAccessibility,
    string TypeParameters,
    string ContextTypeDisplay,
    bool IsAsync,
    EquatableArray<ContainingTypeModel> ContainingTypes,
    EquatableArray<StateModel> States,
    EquatableArray<TriggerModel> Triggers,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    public static StateMachineModel? FromSymbol(
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classSyntax,
        CancellationToken ct)
    {
        var diagnostics = new List<DiagnosticInfo>();

        if (!classSyntax.Modifiers.Any(m => m.ValueText == "partial"))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Descriptors.MachineMustBePartial,
                classSyntax.Identifier.GetLocation(),
                classSymbol.Name));
        }

        var containing = new List<ContainingTypeModel>();
        for (var ct2 = classSymbol.ContainingType; ct2 is not null; ct2 = ct2.ContainingType)
        {
            var decl = ct2.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax(ct))
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault();
            var isPartial = decl is not null && decl.Modifiers.Any(m => m.ValueText == "partial");
            if (!isPartial)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.ContainingTypeMustBePartial,
                    classSyntax.Identifier.GetLocation(),
                    classSymbol.Name,
                    ct2.Name));
            }

            var keyword = ct2.IsRecord
                ? (ct2.TypeKind == TypeKind.Struct ? "record struct" : "record")
                : ct2.TypeKind switch
                {
                    TypeKind.Struct => "struct",
                    TypeKind.Interface => "interface",
                    _ => "class"
                };
            containing.Add(new ContainingTypeModel(
                ct2.Name,
                keyword,
                AccessibilityString(ct2.DeclaredAccessibility),
                TypeParameterList(ct2)));
        }

        containing.Reverse();

        var attribute = classSymbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass is { } ac
            && ac.ToDisplayString() == "Nalu.SharpState.StateMachineDefinitionAttribute");
        var contextType = "global::System.Object";
        var isAsyncMachine = false;
        if (attribute is not null)
        {
            if (attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is INamedTypeSymbol ctxSym)
            {
                contextType = ctxSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            foreach (var kv in attribute.NamedArguments)
            {
                if (kv.Key == "Async" && kv.Value.Value is bool b)
                {
                    isAsyncMachine = b;
                }
            }
        }

        var triggers = ImmutableArray.CreateBuilder<TriggerModel>();
        var seenTriggers = new HashSet<string>();
        var states = new List<StateModel>();
        var seenStates = new HashSet<string>();

        CollectRegion(
            regionClass: classSymbol,
            parentStateName: null,
            accessPrefix: string.Empty,
            isRoot: true,
            isAsyncMachine: isAsyncMachine,
            states,
            seenStates,
            triggers,
            seenTriggers,
            diagnostics,
            ct);

        var ns = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } n
            ? n.ToDisplayString()
            : string.Empty;

        var hintPrefix = string.IsNullOrEmpty(ns) ? string.Empty : ns + ".";
        foreach (var c in containing)
        {
            hintPrefix += c.Name + ".";
        }

        var hintname = hintPrefix + classSymbol.Name + ".g.cs";

        return new StateMachineModel(
            hintname,
            ns,
            classSymbol.Name,
            classSymbol.IsRecord ? "record" : "class",
            AccessibilityString(classSymbol.DeclaredAccessibility),
            TypeParameterList(classSymbol),
            contextType,
            isAsyncMachine,
            new EquatableArray<ContainingTypeModel>(containing.ToImmutableArray()),
            new EquatableArray<StateModel>(states.ToImmutableArray()),
            new EquatableArray<TriggerModel>(triggers.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()));
    }

    private static void CollectRegion(
        INamedTypeSymbol regionClass,
        string? parentStateName,
        string accessPrefix,
        bool isRoot,
        bool isAsyncMachine,
        List<StateModel> states,
        HashSet<string> seenStates,
        ImmutableArray<TriggerModel>.Builder triggers,
        HashSet<string> seenTriggers,
        List<DiagnosticInfo> diagnostics,
        CancellationToken ct)
    {
        CollectTriggers(regionClass, isInSubRegion: !isRoot, isAsyncMachine: isAsyncMachine, triggers, seenTriggers, diagnostics, ct);

        var localStateNames = new HashSet<string>();

        foreach (var member in regionClass.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IPropertySymbol property)
            {
                continue;
            }

            var stateAttr = property.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass is { } ac
                && ac.ToDisplayString() == "Nalu.SharpState.StateDefinitionAttribute");
            if (stateAttr is null)
            {
                continue;
            }

            var propertySyntax = property.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax(ct))
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault();

            var returnTypeName = property.Type.Name;
            if (returnTypeName != "IStateConfiguration")
            {
                var loc = propertySyntax?.Identifier.GetLocation() ?? property.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.StateReturnType,
                    loc,
                    property.Name));
            }

            if (!seenStates.Add(property.Name))
            {
                var loc = propertySyntax?.Identifier.GetLocation() ?? property.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.DuplicateName,
                    loc,
                    "state",
                    property.Name,
                    regionClass.Name));
                continue;
            }

            localStateNames.Add(property.Name);
            var regionPath = accessPrefix.TrimEnd('.');
            states.Add(new StateModel(property.Name, parentStateName, InitialChildState: null, regionPath));
        }

        foreach (var nested in regionClass.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            var subAttr = nested.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass is { } ac
                && ac.ToDisplayString() == "Nalu.SharpState.SubStateMachineAttribute");
            if (subAttr is null)
            {
                continue;
            }

            var nestedSyntax = nested.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax(ct))
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            var isPartial = nestedSyntax is not null && nestedSyntax.Modifiers.Any(m => m.ValueText == "partial");
            if (!isPartial)
            {
                var loc = nestedSyntax?.Identifier.GetLocation() ?? nested.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.SubStateMachineMustBeNestedPartial,
                    loc,
                    nested.Name));
            }

            ParseSubStateMachineArgs(subAttr, out var regionParent, out var regionInitial);

            var attrLocation = subAttr.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation()
                ?? nested.Locations.FirstOrDefault();

            string? validParentForChildren = null;
            if (regionParent is not null)
            {
                if (!localStateNames.Contains(regionParent))
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        Descriptors.SubStateMachineParentScope,
                        attrLocation,
                        nested.Name,
                        regionParent));
                }
                else
                {
                    validParentForChildren = regionParent;
                    var parentIndex = states.FindIndex(s => s.Name == regionParent);
                    if (parentIndex >= 0 && regionInitial is not null && states[parentIndex].InitialChildState is null)
                    {
                        states[parentIndex] = states[parentIndex] with { InitialChildState = regionInitial };
                    }
                }
            }

            var nestedAccessPrefix = accessPrefix + nested.Name + ".";

            CollectRegion(
                regionClass: nested,
                parentStateName: validParentForChildren,
                accessPrefix: nestedAccessPrefix,
                isRoot: false,
                isAsyncMachine: isAsyncMachine,
                states,
                seenStates,
                triggers,
                seenTriggers,
                diagnostics,
                ct);

            // Validate that `initial` names a state that was just collected inside this region (direct child).
            if (regionInitial is not null && validParentForChildren is not null)
            {
                var initialFound = states.Any(s => s.Name == regionInitial && s.ParentState == validParentForChildren);
                if (!initialFound)
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        Descriptors.SubStateMachineInitialScope,
                        attrLocation,
                        nested.Name,
                        regionInitial));
                }
            }
        }
    }

    private static void CollectTriggers(
        INamedTypeSymbol classSymbol,
        bool isInSubRegion,
        bool isAsyncMachine,
        ImmutableArray<TriggerModel>.Builder triggers,
        HashSet<string> seenTriggers,
        List<DiagnosticInfo> diagnostics,
        CancellationToken ct)
    {
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            var triggerAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass is { } ac
                && ac.ToDisplayString() == "Nalu.SharpState.StateTriggerDefinitionAttribute");
            if (triggerAttr is null)
            {
                continue;
            }

            var methodSyntax = method.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax(ct))
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (isInSubRegion)
            {
                var loc = methodSyntax?.Identifier.GetLocation() ?? method.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.TriggerInSubStateMachine,
                    loc,
                    method.Name,
                    classSymbol.Name));
                continue;
            }

            if (!method.IsPartialDefinition || method.ReturnType.SpecialType != SpecialType.System_Void)
            {
                var loc = methodSyntax?.Identifier.GetLocation() ?? method.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.TriggerMustBePartialVoid,
                    loc,
                    method.Name));
            }

            if (isAsyncMachine && method.Name.EndsWith("Async", StringComparison.Ordinal))
            {
                var loc = methodSyntax?.Identifier.GetLocation() ?? method.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.TriggerNameAsyncSuffix,
                    loc,
                    method.Name));
            }

            if (!seenTriggers.Add(method.Name))
            {
                var loc = methodSyntax?.Identifier.GetLocation() ?? method.Locations.FirstOrDefault();
                diagnostics.Add(DiagnosticInfo.Create(
                    Descriptors.DuplicateName,
                    loc,
                    "trigger",
                    method.Name,
                    classSymbol.Name));
                continue;
            }

            var parameters = method.Parameters
                .Select(p => new ParameterModel(
                    p.Name,
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                .ToImmutableArray();

            triggers.Add(new TriggerModel(
                method.Name,
                new EquatableArray<ParameterModel>(parameters)));
        }
    }

    private static void ParseSubStateMachineArgs(AttributeData attr, out string? parent, out string? initial)
    {
        parent = null;
        initial = null;

        var syntaxRef = attr.ApplicationSyntaxReference;
        if (syntaxRef?.GetSyntax() is AttributeSyntax attrSyntax && attrSyntax.ArgumentList is { } argList)
        {
            var positional = 0;
            foreach (var arg in argList.Arguments)
            {
                var nameColon = arg.NameColon?.Name.Identifier.ValueText;
                var nameEquals = arg.NameEquals?.Name.Identifier.ValueText;
                var name = nameColon ?? nameEquals;
                var value = ExtractIdentifierTail(arg.Expression);

                string? target;
                if (name is null)
                {
                    target = positional switch
                    {
                        0 => "parent",
                        1 => "initial",
                        _ => null
                    };
                    positional++;
                }
                else
                {
                    target = name.ToLowerInvariant() switch
                    {
                        "parent" => "parent",
                        "initial" => "initial",
                        _ => null
                    };
                }

                if (target == "parent" && value is not null)
                {
                    parent = value;
                }
                else if (target == "initial" && value is not null)
                {
                    initial = value;
                }
            }
        }

        if (parent is null && attr.ConstructorArguments.Length > 0)
        {
            parent = ExtractEnumName(attr.ConstructorArguments[0]);
        }
        if (initial is null && attr.ConstructorArguments.Length > 1)
        {
            initial = ExtractEnumName(attr.ConstructorArguments[1]);
        }
    }

    private static string? ExtractIdentifierTail(ExpressionSyntax expr) => expr switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
        IdentifierNameSyntax i => i.Identifier.ValueText,
        _ => null
    };

    private static string? ExtractEnumName(TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Enum && constant.Type is INamedTypeSymbol enumType && constant.Value is { } value)
        {
            foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.ConstantValue is not null && field.ConstantValue.Equals(value))
                {
                    return field.Name;
                }
            }
        }
        return null;
    }

    private static string AccessibilityString(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        _ => "internal"
    };

    private static string TypeParameterList(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
        {
            return string.Empty;
        }

        return "<" + string.Join(", ", type.TypeParameters.Select(tp => tp.Name)) + ">";
    }
}
