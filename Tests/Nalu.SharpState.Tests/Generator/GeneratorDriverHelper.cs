using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nalu.SharpState.Generators;

namespace Nalu.SharpState.Tests.Generator;

internal static class GeneratorDriverHelper
{
    private static readonly ImmutableArray<MetadataReference> _references = BuildReferences();

    public static GeneratorDriverRunResult RunGenerator(string source, out Compilation outputCompilation)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTestAssembly",
            syntaxTrees: [syntaxTree],
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new StateMachineGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: (CSharpParseOptions) syntaxTree.Options,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out _);
        return driver.GetRunResult();
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trustedAssembliesPaths = ((string?) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var refs = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var path in trustedAssembliesPaths)
        {
            refs.Add(MetadataReference.CreateFromFile(path));
        }

        refs.Add(MetadataReference.CreateFromFile(typeof(StateMachineDefinitionAttribute).Assembly.Location));
        return refs.ToImmutable();
    }

    public static VerifySettings DefaultSettings([CallerFilePath] string? sourceFilePath = null)
    {
        var settings = new VerifySettings();
        settings.UseDirectory("Snapshots");
        return settings;
    }
}
