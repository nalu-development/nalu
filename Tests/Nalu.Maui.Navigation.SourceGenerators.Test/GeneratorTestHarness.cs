using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nalu.Maui.Navigation.SourceGenerators;

namespace Nalu.Maui.Test.SourceGenerators;

/// <summary>
/// Black-box harness: compiles user source against minimal MAUI/Nalu stubs (the generator
/// matches types by name, so real MAUI references are unnecessary), runs the generator, and
/// exposes the generated text, the generator diagnostics, and the OUTPUT compilation
/// diagnostics (proving the generated code compiles).
/// </summary>
internal static class GeneratorTestHarness
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
            public abstract class Page : Element;
            public class ContentPage : Page;
        }

        namespace Nalu
        {
            public class NavigationConfigurator
            {
                public NavigationConfigurator AddPage<TPage>() => this;
                public NavigationConfigurator AddPage<TPageModel, TPage>() => this;
                public NavigationConfigurator AddPage<TPageModel, TPageModelImplementation, TPage>() => this;
            }

            public sealed class NavigationRestoreOptions
            {
                public NavigationRestoreOptions AddIntent<T>(string? typeId = null) => this;
            }

            public interface IEnteringAware<in TIntent>;
            public interface IAppearingAware<in TIntent>;
            public interface IAwaitableIntentController;
            public abstract class AwaitableIntent : IAwaitableIntentController;
            public abstract class AwaitableIntent<T> : IAwaitableIntentController;

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
            public sealed class AutoNavigationIntentAttribute(string? typeId = null) : System.Attribute
            {
                public string? Id { get; } = typeId;
                public bool Enabled { get; set; } = true;
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class AutoNavigationPageAttribute : System.Attribute
            {
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
        ImmutableArray<Diagnostic> OutputCompilationErrors,
        GeneratorDriver Driver,
        CSharpCompilation InputCompilation
    );

    public static CSharpCompilation CreateCompilation(params string[] sources)
        => CSharpCompilation.Create(
            "TestApp",
            [.. sources.Select(static s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Latest)))],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );

    public static Result Run(string userSource, bool includeStubs = true)
    {
        var compilation = includeStubs ? CreateCompilation(Stubs, userSource) : CreateCompilation(userSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NavigationRegistrationGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics, TestContext.Current.CancellationToken);

        var generated = driver.GetRunResult()
                              .Results
                              .SelectMany(static r => r.GeneratedSources)
                              .SingleOrDefault(static s => s.HintName == "NaluNavigationRegistrations.g.cs")
                              .SourceText?.ToString()
                        ?? string.Empty;

        var outputErrors = outputCompilation
                           .GetDiagnostics(TestContext.Current.CancellationToken)
                           .Where(static d => d.Severity == DiagnosticSeverity.Error)
                           .ToImmutableArray();

        return new Result(generated, generatorDiagnostics, outputErrors, driver, compilation);
    }
}
