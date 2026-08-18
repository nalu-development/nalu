using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nalu.Maui.Test.SourceGenerators;

namespace Nalu.Maui.Scaffold.SourceGenerators.Test;

public class ScaffoldKeyboardManagerAnalyzerTests
{
    private const string _coreStubs =
        """
        namespace Nalu
        {
            public enum SoftKeyboardAdjustMode { Pan, Resize, None }

            public static class NaluCoreMauiAppBuilderExtensions
            {
                public static MauiAppBuilderStub UseNaluSoftKeyboardManager(this MauiAppBuilderStub builder, SoftKeyboardAdjustMode defaultAdjustMode = SoftKeyboardAdjustMode.Resize) => builder;
            }

            public class MauiAppBuilderStub;
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(bool referencesScaffold, string userSource)
    {
        var sources = referencesScaffold ? new[] { ScaffoldGeneratorTestHarness.Stubs, _coreStubs, userSource } : [_coreStubs, userSource];
        var compilation = ScaffoldGeneratorTestHarness.CreateCompilation(sources);

        var withAnalyzers = compilation.WithAnalyzers([new ScaffoldKeyboardManagerAnalyzer()]);

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    private const string _usage =
        """
        using Nalu;

        namespace MyApp;

        public static class Program
        {
            public static void Configure(MauiAppBuilderStub builder) => builder.UseNaluSoftKeyboardManager();
        }
        """;

    [Fact(DisplayName = "NALU0104: UseNaluSoftKeyboardManager is an error in an app referencing the scaffold")]
    public async Task ReportsErrorWhenScaffoldIsReferenced()
    {
        var diagnostics = await AnalyzeAsync(referencesScaffold: true, _usage);

        diagnostics.Should().ContainSingle(d => d.Id == "NALU0104")
                   .Which.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact(DisplayName = "NALU0104 stays silent without the scaffold")]
    public async Task SilentWithoutScaffold()
    {
        var diagnostics = await AnalyzeAsync(referencesScaffold: false, _usage);

        diagnostics.Should().NotContain(d => d.Id == "NALU0104");
    }

    [Fact(DisplayName = "NALU0104 ignores unrelated methods of the same name outside the Nalu namespace")]
    public async Task IgnoresForeignMethodsWithTheSameName()
    {
        var diagnostics = await AnalyzeAsync(
            referencesScaffold: true,
            """
            namespace MyApp;

            public static class Other
            {
                public static void UseNaluSoftKeyboardManager() { }
                public static void Configure() => UseNaluSoftKeyboardManager();
            }
            """
        );

        diagnostics.Should().NotContain(d => d.Id == "NALU0104");
    }
}
