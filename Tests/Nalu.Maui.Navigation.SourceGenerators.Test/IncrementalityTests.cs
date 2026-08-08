using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nalu.Maui.Navigation.SourceGenerators;

namespace Nalu.Maui.Test.SourceGenerators;

/// <summary>
/// Incrementality is a tested invariant, not a hope: an edit unrelated to pages/models must
/// leave every tracked pipeline output cached, so the IDE never pays full regeneration cost
/// per keystroke.
/// </summary>
public class IncrementalityTests
{
    private const string _pageSource =
        """
        using Microsoft.Maui.Controls;
        using System.ComponentModel;

        namespace MyApp;

        public class DetailViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class DetailPage : ContentPage
        {
            public DetailPage(DetailViewModel model)
            {
                BindingContext = model;
            }
        }
        """;

    [Fact(DisplayName = "An unrelated edit keeps all pipeline outputs cached")]
    public void UnrelatedEditKeepsOutputsCached()
    {
        var compilation = GeneratorTestHarness.CreateCompilation(GeneratorTestHarness.Stubs, _pageSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NavigationRegistrationGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
        );

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Unrelated edit: a brand-new syntax tree with no pages or models in it.
        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace MyApp { public static class Helper { public static int X => 42; } }",
                new CSharpParseOptions(LanguageVersion.Latest),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult().Results.Single();

        foreach (var step in new[] { "Pages", "Models", "Registrations" })
        {
            result.TrackedSteps[step]
                  .SelectMany(static s => s.Outputs)
                  .Should()
                  .OnlyContain(
                      static o => o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged,
                      "step '{0}' must not recompute on unrelated edits", step
                  );
        }
    }

    [Fact(DisplayName = "Editing a page recomputes and changes the generated output")]
    public void PageEditRecomputes()
    {
        var compilation = GeneratorTestHarness.CreateCompilation(GeneratorTestHarness.Stubs, _pageSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NavigationRegistrationGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
        );

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        var edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace MyApp { public class ExtraPage : Microsoft.Maui.Controls.ContentPage; }",
                new CSharpParseOptions(LanguageVersion.Latest),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        driver.GetRunResult()
              .Results
              .Single()
              .GeneratedSources
              .Single()
              .SourceText
              .ToString()
              .Should()
              .Contain("navigation.AddPage<global::MyApp.ExtraPage>();");
    }
}
