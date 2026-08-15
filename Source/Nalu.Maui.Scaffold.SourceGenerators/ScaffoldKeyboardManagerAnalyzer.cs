using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Nalu.Maui.Scaffold.SourceGenerators;

/// <summary>
/// Reports <c>NALU0104</c> when an app that references Nalu.Maui.Scaffold enables
/// <c>Nalu.Maui.Core</c>'s <c>UseNaluSoftKeyboardManager</c>. The scaffold owns the soft-keyboard
/// geometry (it disconnects MAUI's iOS keyboard manager and drives Android through IME window
/// insets under an edge-to-edge, adjustResize window); the Core manager fights that by re-padding
/// the page controller / rewriting the window's soft-input mode, so the combination is unsupported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ScaffoldKeyboardManagerAnalyzer : DiagnosticAnalyzer
{
    private const string _methodName = "UseNaluSoftKeyboardManager";
    private const string _scaffoldMarkerType = "Nalu.IScaffoldConfigurator";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Diagnostics.SoftKeyboardManagerUnsupported);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            // Only apps that reference the scaffold: the analyzer ships inside its package, but a
            // transitive reference (or a shared build props file) can bring it further than that.
            if (compilationContext.Compilation.GetTypeByMetadataName(_scaffoldMarkerType) is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(static operationContext =>
            {
                var invocation = (IInvocationOperation)operationContext.Operation;
                var method = invocation.TargetMethod;

                if (method.Name == _methodName
                    && method.ContainingNamespace is { Name: "Nalu", ContainingNamespace.IsGlobalNamespace: true })
                {
                    operationContext.ReportDiagnostic(Diagnostic.Create(Diagnostics.SoftKeyboardManagerUnsupported, invocation.Syntax.GetLocation()));
                }
            }, OperationKind.Invocation);
        });
    }
}
