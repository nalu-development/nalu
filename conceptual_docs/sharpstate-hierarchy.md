# Hierarchical State Machines

Real-world behavior rarely fits into a flat list of states. Think of a network client: while `Connected`, it can be `Authenticating`, `Authenticated`, or `Browsing`; while `Authenticated`, it can be `Browsing` or `Editing`. `Nalu.SharpState` models these hierarchies through **sub-state-machine regions**, declared as nested partial classes with the `[SubStateMachine]` attribute.

## The `[SubStateMachine]` attribute

A region is a `partial class` nested inside either the root `[StateMachineDefinition]` class or another `[SubStateMachine]` region. It refines a **composite state** declared in its immediately enclosing scope.

```csharp
[SubStateMachine(parent: State.Connected)]
private partial class ConnectedRegion
{
    [StateDefinition(Initial = true)]
    private static IStateConfiguration Authenticating => ConfigureState()
        .OnAuthOk(t => t.Target(State.Authenticated));

    [StateDefinition]
    private static IStateConfiguration Authenticated => ConfigureState()
        .OnMessage(t => t
            .Stay()
            .Invoke((ctx, text) => ctx.Log.Add(text)));
}
```

The attribute takes one mandatory constructor argument:

| Argument | Meaning | Scoping rule |
|----------|---------|--------------|
| `parent` | The composite state this region refines. | Must be a `[StateDefinition]` declared in the immediately enclosing class (root or outer region). |

Every `[StateDefinition]` inside the class is treated as a child of `parent`. Exactly one state in the region must be marked with `[StateDefinition(Initial = true)]`. Entering `parent` automatically resolves to that initial child; if it is itself a composite, resolution continues until a real leaf is reached.

> **Triggers must live on the root machine.** Placing `[StateTriggerDefinition]` inside a `[SubStateMachine]` class is a build error (`NSS009`). Regions describe structure, not new inputs.

## A two-level example

```csharp
using Nalu.SharpState;

public class NetworkContext
{
    public List<string> Log { get; } = [];
}

[StateMachineDefinition(typeof(NetworkContext))]
public partial class NetworkMachine
{
    [StateTriggerDefinition] static partial void Connect();
    [StateTriggerDefinition] static partial void Disconnect();
    [StateTriggerDefinition] static partial void AuthOk();
    [StateTriggerDefinition] static partial void Message(string text);
    [StateTriggerDefinition] static partial void StartEdit();
    [StateTriggerDefinition] static partial void Save();

    [StateDefinition(Initial = true)]
    private static IStateConfiguration Idle => ConfigureState()
        .OnConnect(t => t.Target(State.Connected));

    [StateDefinition]
    private static IStateConfiguration Connected => ConfigureState()
        .OnDisconnect(t => t.Target(State.Idle));

    [SubStateMachine(parent: State.Connected)]
    private partial class ConnectedRegion
    {
        [StateDefinition(Initial = true)]
        private static IStateConfiguration Authenticating => ConfigureState()
            .OnAuthOk(t => t.Target(State.Authenticated));

        [StateDefinition]
        private static IStateConfiguration Authenticated => ConfigureState()
            .OnMessage(t => t.Stay().Invoke((ctx, msg) => ctx.Log.Add(msg)));

        [SubStateMachine(parent: State.Authenticated)]
        private partial class AuthenticatedRegion
        {
            [StateDefinition(Initial = true)]
            private static IStateConfiguration Browsing => ConfigureState()
                .OnStartEdit(t => t.Target(State.Editing));

            [StateDefinition]
            private static IStateConfiguration Editing => ConfigureState()
                .OnSave(t => t.Target(State.Browsing));
        }
    }
}
```

The hierarchy this produces:

```
Idle
Connected
├── Authenticating
└── Authenticated
    ├── Browsing
    └── Editing
```

## Entry, inheritance, and leaf resolution

Three rules govern how the runtime walks this tree.

### 1. Targeting a composite resolves to its initial leaf

```csharp
var machine = NetworkMachine.CreateActor(new NetworkContext());

machine.Connect();
// Target(Connected) -> initial Authenticating -> Authenticating has no deeper initial
machine.CurrentState.Should().Be(NetworkMachine.State.Authenticating);

machine.AuthOk();
// Target(Authenticated) -> initial Browsing -> Browsing is a leaf
machine.CurrentState.Should().Be(NetworkMachine.State.Browsing);
machine.IsIn(NetworkMachine.State.Authenticated).Should().BeTrue();
machine.IsIn(NetworkMachine.State.Connected).Should().BeTrue();
```

`CurrentState` always reports the **leaf** the machine settled on. Use `IsIn(...)` to test membership of a composite.

### 2. Transitions are inherited from ancestors

When a trigger fires, the engine walks **up** from the current leaf looking for a matching transition. The first ancestor that declares one (and whose guard, if any, passes) wins.

```csharp
// Current state: Editing (deep leaf, 3 levels down)
var machine = NetworkMachine.CreateActor(NetworkMachine.State.Editing, new NetworkContext());

machine.Disconnect();
// Editing -> Authenticated -> Connected: Connected handles Disconnect -> target Idle
machine.CurrentState.Should().Be(NetworkMachine.State.Idle);
machine.IsIn(NetworkMachine.State.Connected).Should().BeFalse();
```

Any state may override an inherited transition by declaring its own `.On<Trigger>(...)` — the closer handler wins.

### 3. `IsIn` respects the hierarchy

```csharp
machine.IsIn(NetworkMachine.State.Editing);      // exact leaf match
machine.IsIn(NetworkMachine.State.Authenticated); // ancestor
machine.IsIn(NetworkMachine.State.Connected);     // ancestor
machine.IsIn(NetworkMachine.State.Idle);          // sibling: false
```

This is how you write predicates like *"can we Save right now?"* without caring which leaf of `Authenticated` we are in.

## Accessing states across regions

Every region sees the single `State` enum generated for the root machine. Inside `AuthenticatedRegion` you can still write `State.Connected` or `State.Idle` — the generator emits the enum on the outer machine class and every nested partial shares it.

```csharp
[SubStateMachine(parent: State.Authenticated)]
private partial class AuthenticatedRegion
{
    [StateDefinition(Initial = true)]
    private static IStateConfiguration Editing => ConfigureState()
        .OnSave(t => t.Target(State.Browsing))
        // ancestor handles Disconnect, so no need to re-declare it here
        ;
}
```

## Scoping rules enforced by the generator

Sub-state machines only make sense when they describe a strict containment tree. The generator enforces this at compile time:

- `parent` **must** be a state declared in the immediately enclosing region. Pointing to a distant or sibling state produces `NSS007`.
- Every region **must** declare exactly one `[StateDefinition(Initial = true)]`. Missing one produces `NSS008`; declaring more than one produces `NSS010`.
- The region class must be `partial` and nested in a valid container (root `[StateMachineDefinition]` or another `[SubStateMachine]`). Free-standing regions produce `NSS005`.
- `[StateTriggerDefinition]` is not allowed inside a region — add it on the root class instead. Violations produce `NSS009`.

See [Diagnostics & Troubleshooting](sharpstate-diagnostics.md) for the full list.

## When to use hierarchy

Regions pay off when:

- You have **shared transitions** that apply to a whole sub-tree (like `Disconnect` on `Connected`).
- A composite has a clear **default entry point** that should be resolved automatically.
- Two or more states form a cluster that would otherwise duplicate the same `On<Trigger>` handlers.

If your state graph is flat and every transition is local, stay flat — adding regions adds cognitive overhead without benefit.
