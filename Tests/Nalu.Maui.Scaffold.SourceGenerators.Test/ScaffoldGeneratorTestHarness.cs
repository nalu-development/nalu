using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Nalu.Maui.Scaffold.SourceGenerators;

namespace Nalu.Maui.Test.SourceGenerators;

/// <summary>
/// Black-box harness for the scaffold overlay generator: compiles user source against minimal
/// MAUI/Nalu stubs, runs the generator, and exposes the generated text, generator diagnostics,
/// and output-compilation errors (proving the generated code compiles).
/// </summary>
internal static class ScaffoldGeneratorTestHarness
{
    public const string Stubs =
        """
        #nullable enable
        namespace Microsoft.Maui.Controls
        {
            public abstract class BindableObject
            {
                public object? BindingContext { get; set; }
            }

            public abstract class Element : BindableObject;
            public class View : Element;
            public class ContentView : View;
        }

        namespace Nalu
        {
            public interface IOverlayRef
            {
                System.Threading.Tasks.Task CloseAsync();
            }

            public interface IScaffoldConfigurator
            {
                IScaffoldConfigurator AddOverlay<TModel, TView>()
                    where TModel : class
                    where TView : Microsoft.Maui.Controls.View;

                IScaffoldConfigurator AddOverlay<TView>()
                    where TView : Microsoft.Maui.Controls.View;
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class AutoOverlayAttribute(System.Type? viewType = null) : System.Attribute
            {
                public System.Type? ViewType { get; } = viewType;
                public bool Enabled { get; set; } = true;
            }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> _references =
        [
            .. ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
               .Split(Path.PathSeparator)
               .Where(static path => !string.IsNullOrEmpty(path))
               .Select(static path => (MetadataReference) MetadataReference.CreateFromFile(path))
        ];

    public sealed record Result(
        string GeneratedText,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<Diagnostic> OutputCompilationErrors
    );

    public static CSharpCompilation CreateCompilation(params string[] sources)
        => CSharpCompilation.Create(
            "TestApp",
            [.. sources.Select(static s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Latest)))],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );

    /// <summary>An in-memory .xaml AdditionalFile, as MAUI's MauiXaml→AdditionalFiles wiring provides them.</summary>
    public sealed class XamlFile(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }

    public static Result Run(string userSource, bool includeStubs = true, XamlFile[]? xamlFiles = null)
    {
        var compilation = includeStubs ? CreateCompilation(Stubs, userSource) : CreateCompilation(userSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ScaffoldOverlayGenerator().AsSourceGenerator()],
            additionalTexts: xamlFiles is null ? [] : [.. xamlFiles],
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics, TestContext.Current.CancellationToken);

        var generated = driver.GetRunResult()
                              .Results
                              .SelectMany(static r => r.GeneratedSources)
                              .SingleOrDefault(static s => s.HintName == "NaluScaffoldRegistrations.g.cs")
                              .SourceText?.ToString()
                        ?? string.Empty;

        var outputErrors = outputCompilation
                           .GetDiagnostics(TestContext.Current.CancellationToken)
                           .Where(static d => d.Severity == DiagnosticSeverity.Error)
                           .ToImmutableArray();

        return new Result(generated, generatorDiagnostics, outputErrors);
    }
}
