using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Nalu.SharpState.Tests.Generator;

[UsesVerify]
public class SnapshotTests
{
    [Fact]
    public Task Flat_machine()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx
        {
            public string? DeviceId { get; set; }
        }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class Flat
        {
            [StateTriggerDefinition] static partial void Connect(string deviceId);
            [StateTriggerDefinition] static partial void Disconnect();

            [StateDefinition]
            private static IStateConfiguration Idle => ConfigureState()
                .OnConnect(t => t.Target(State.Connected).Invoke((ctx, id) => ctx.DeviceId = id));

            [StateDefinition]
            private static IStateConfiguration Connected => ConfigureState()
                .OnDisconnect(t => t.Target(State.Idle));
        }
        """;
        var result = GeneratorDriverHelper.RunGenerator(source, out _);
        return Verify(result).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Reaction_callback()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class ReactionMachine
        {
            [StateTriggerDefinition] static partial void Start();
            [StateTriggerDefinition] static partial void Sync(long revision);

            [StateDefinition]
            private static IStateConfiguration Idle => ConfigureState()
                .OnStart(t => t.Target(State.Running));

            [StateDefinition]
            private static IStateConfiguration Running => ConfigureState()
                .OnSync(t => t.Stay().ReactAsync((_, _) => default));
        }
        """;
        var result = GeneratorDriverHelper.RunGenerator(source, out _);
        return Verify(result).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Hierarchical_machine()
    {
        var source = """
        using Nalu.SharpState;
        using System.Collections.Generic;

        namespace Sample;

        public class Ctx
        {
            public List<string> Inbox { get; } = new();
        }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class Hier
        {
            [StateTriggerDefinition] static partial void Connect();
            [StateTriggerDefinition] static partial void Disconnect();
            [StateTriggerDefinition] static partial void AuthOk();
            [StateTriggerDefinition] static partial void Message(string m);

            [StateDefinition]
            private static IStateConfiguration Idle => ConfigureState()
                .OnConnect(t => t.Target(State.Connected));

            [StateDefinition]
            private static IStateConfiguration Connected => ConfigureState()
                .OnDisconnect(t => t.Target(State.Idle));

            [SubStateMachine(parent: State.Connected, initial: State.Authenticating)]
            private partial class ConnectedRegion
            {
                [StateDefinition]
                private static IStateConfiguration Authenticating => ConfigureState()
                    .OnAuthOk(t => t.Target(State.Authenticated));

                [StateDefinition]
                private static IStateConfiguration Authenticated => ConfigureState()
                    .OnMessage(t => t.Stay().Invoke((ctx, m) => ctx.Inbox.Add(m)));
            }
        }
        """;
        var result = GeneratorDriverHelper.RunGenerator(source, out _);
        return Verify(result).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Nested_regions_machine()
    {
        var source = """
        using Nalu.SharpState;
        using System.Collections.Generic;

        namespace Sample;

        public class Ctx
        {
            public List<string> Log { get; } = new();
        }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class Deep
        {
            [StateTriggerDefinition] static partial void Connect();
            [StateTriggerDefinition] static partial void Disconnect();
            [StateTriggerDefinition] static partial void AuthOk();
            [StateTriggerDefinition] static partial void StartEdit();
            [StateTriggerDefinition] static partial void Save();

            [StateDefinition]
            private static IStateConfiguration Idle => ConfigureState()
                .OnConnect(t => t.Target(State.Connected));

            [StateDefinition]
            private static IStateConfiguration Connected => ConfigureState()
                .OnDisconnect(t => t.Target(State.Idle));

            [SubStateMachine(parent: State.Connected, initial: State.Authenticating)]
            private partial class ConnectedRegion
            {
                [StateDefinition]
                private static IStateConfiguration Authenticating => ConfigureState()
                    .OnAuthOk(t => t.Target(State.Authenticated));

                [StateDefinition]
                private static IStateConfiguration Authenticated => ConfigureState();

                [SubStateMachine(parent: State.Authenticated, initial: State.Browsing)]
                private partial class AuthenticatedRegion
                {
                    [StateDefinition]
                    private static IStateConfiguration Browsing => ConfigureState()
                        .OnStartEdit(t => t.Target(State.Editing));

                    [StateDefinition]
                    private static IStateConfiguration Editing => ConfigureState()
                        .OnSave(t => t.Target(State.Browsing));
                }
            }
        }
        """;
        var result = GeneratorDriverHelper.RunGenerator(source, out _);
        return Verify(result).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Nested_namespaced_machine()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample.Nested.Deep;

        public class Ctx { }

        public partial class Outer
        {
            [StateMachineDefinition(typeof(Ctx))]
            internal partial class Machine
            {
                [StateTriggerDefinition] static partial void Go();

                [StateDefinition]
                private static IStateConfiguration A => ConfigureState()
                    .OnGo(t => t.Target(State.B));

                [StateDefinition]
                private static IStateConfiguration B => ConfigureState();
            }
        }
        """;
        var result = GeneratorDriverHelper.RunGenerator(source, out _);
        return Verify(result).UseDirectory("Snapshots");
    }

    [Fact]
    public Task Lifecycle_hooks_and_ignore()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx
        {
            public int Entries { get; set; }
            public int Exits { get; set; }
        }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class Hooks
        {
            [StateTriggerDefinition] static partial void Start();
            [StateTriggerDefinition] static partial void Ping();

            [StateDefinition]
            private static IStateConfiguration Idle => ConfigureState()
                .OnExit(ctx => ctx.Exits++)
                .OnStart(t => t.Target(State.Running));

            [StateDefinition]
            private static IStateConfiguration Running => ConfigureState()
                .OnEntry(ctx => ctx.Entries++)
                .OnPing(t => t.Ignore());
        }
        """;
        var result = GeneratorDriverHelper.RunGenerator(source, out _);
        return Verify(result).UseDirectory("Snapshots");
    }

    [Fact]
    public void Incrementality_second_run_is_cached()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class M
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition]
            private static IStateConfiguration A => ConfigureState()
                .OnGo(t => t.Target(State.B));

            [StateDefinition]
            private static IStateConfiguration B => ConfigureState();
        }
        """;
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            source,
            new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview));
        var trustedAssembliesPaths = ((string?) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var refs = new List<MetadataReference>();
        foreach (var path in trustedAssembliesPaths)
        {
            refs.Add(MetadataReference.CreateFromFile(path));
        }
        refs.Add(MetadataReference.CreateFromFile(typeof(StateMachineDefinitionAttribute).Assembly.Location));

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            refs,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Nalu.SharpState.Generators.StateMachineGenerator();
        GeneratorDriver driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions) syntaxTree.Options,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);

        var clone = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            source,
            (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions) syntaxTree.Options);
        var compilation2 = compilation.ReplaceSyntaxTree(syntaxTree, clone);

        driver = driver.RunGenerators(compilation2);

        var result = driver.GetRunResult();
        var allSteps = result.Results[0].TrackedSteps.SelectMany(kv => kv.Value).ToList();
        allSteps.Should().NotBeEmpty();
        allSteps.Any(step => step.Outputs.Any(o => o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged))
            .Should().BeTrue();
    }
}

public sealed class UsesVerifyAttribute : Attribute
{
}
