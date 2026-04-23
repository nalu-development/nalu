using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Nalu.SharpState.Tests.Generator;

public class DiagnosticTests
{
    private static IReadOnlyList<Diagnostic> GetDiagnostics(string source) => GeneratorDriverHelper.RunGenerator(source, out _).Diagnostics;

    [Fact]
    public void NSS001_reported_when_class_is_not_partial()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public class NotPartial
        {
            [StateTriggerDefinition] static partial void Go();
            [StateDefinition] private static IStateConfiguration A => ConfigureState();
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS001");
    }

    [Fact]
    public void NSS002_reported_on_duplicate_trigger()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class DupTrigger
        {
            [StateTriggerDefinition] static partial void Go();
            [StateTriggerDefinition] static partial void Go(int x);

            [StateDefinition] private static IStateConfiguration A => ConfigureState();
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS002");
    }

    [Fact]
    public void NSS003_reported_when_state_property_has_wrong_return_type()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class WrongState
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition] private static string A => "nope";
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS003");
    }

    [Fact]
    public void NSS003_not_reported_for_generic_runtime_interface_return_type()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class GenericConfigReturnType
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition]
            private static global::Nalu.SharpState.IStateConfiguration<Ctx, State, Trigger> A => ConfigureState();
        }
        """;

        GetDiagnostics(source).Should().NotContain(d => d.Id == "NSS003");
    }

    [Fact]
    public void NSS004_reported_when_trigger_is_not_partial_void()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class BadTrigger
        {
            [StateTriggerDefinition] static void Go() { }
            [StateDefinition] private static IStateConfiguration A => ConfigureState();
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS004");
    }

    [Fact]
    public void NSS005_reported_when_substatemachine_is_not_partial()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class M
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition] private static IStateConfiguration A => ConfigureState();
            [StateDefinition] private static IStateConfiguration B => ConfigureState();

            [SubStateMachine(parent: State.A, initial: State.B)]
            private class NotPartialRegion { }
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS005");
    }

    [Fact]
    public void NSS006_reported_when_containing_type_is_not_partial()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        public class Outer
        {
            [StateMachineDefinition(typeof(Ctx))]
            public partial class M
            {
                [StateTriggerDefinition] static partial void Go();
                [StateDefinition] private static IStateConfiguration A => ConfigureState();
            }
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS006");
    }

    [Fact]
    public void NSS007_reported_when_parent_is_not_in_enclosing_region()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class M
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition] private static IStateConfiguration A => ConfigureState();

            [SubStateMachine(parent: State.Nowhere, initial: State.B)]
            private partial class Region
            {
                [StateDefinition] private static IStateConfiguration B => ConfigureState();
            }
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS007");
    }

    [Fact]
    public void NSS008_reported_when_initial_is_not_defined_inside_region()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class M
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition] private static IStateConfiguration A => ConfigureState();

            [SubStateMachine(parent: State.A, initial: State.Ghost)]
            private partial class Region
            {
                [StateDefinition] private static IStateConfiguration B => ConfigureState();
            }
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS008");
    }

    [Fact]
    public void NSS009_reported_when_trigger_is_declared_inside_substatemachine()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx))]
        public partial class M
        {
            [StateTriggerDefinition] static partial void Go();

            [StateDefinition] private static IStateConfiguration A => ConfigureState();

            [SubStateMachine(parent: State.A, initial: State.B)]
            private partial class Region
            {
                [StateTriggerDefinition] static partial void Nope();
                [StateDefinition] private static IStateConfiguration B => ConfigureState();
            }
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS009");
    }

    [Fact]
    public void NSS010_reported_when_async_trigger_ends_with_Async_suffix()
    {
        var source = """
        using Nalu.SharpState;

        namespace Sample;

        public class Ctx { }

        [StateMachineDefinition(typeof(Ctx), Async = true)]
        public partial class M
        {
            [StateTriggerDefinition] static partial void InspectAsync();

            [StateDefinition] private static IStateConfiguration A => ConfigureState();
        }
        """;
        GetDiagnostics(source).Should().Contain(d => d.Id == "NSS010");
    }
}
