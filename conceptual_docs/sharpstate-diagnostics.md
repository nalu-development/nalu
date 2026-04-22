# Diagnostics & Troubleshooting

The source generator emits targeted diagnostics instead of generating incorrect code. All of them live under the `Nalu.SharpState` category and share the `NSS00x` prefix, so you can filter or escalate them via `.editorconfig`:

```ini
# .editorconfig
dotnet_diagnostic.NSS001.severity = error
```

Every diagnostic listed below is reported as **error** by default.

## Generator diagnostics

### `NSS001` — State machine class must be partial

```
Class 'DoorMachine' is marked with [StateMachineDefinition] but is not declared as partial
```

Add `partial` to the class declaration. The generator needs to contribute the `State`/`Trigger` enums, the `Actor`, and the `CreateActor` factory to the same type.

```csharp
// Wrong
[StateMachineDefinition(typeof(DoorContext))]
public class DoorMachine { ... }

// Right
[StateMachineDefinition(typeof(DoorContext))]
public partial class DoorMachine { ... }
```

### `NSS002` — Duplicate trigger or state name

```
Duplicate state name 'Opened' in state machine 'DoorMachine'
```

Trigger names feed the `Trigger` enum, state property names feed the `State` enum. Each name must be unique **across** triggers and states of the same machine.

### `NSS003` — Invalid state property return type

```
[StateDefinition] property 'Idle' must return the machine's IStateConfiguration type
```

`[StateDefinition]` properties must return the machine-scoped `IStateConfiguration` nested interface. Always build them with `ConfigureState()`:

```csharp
[StateDefinition]
private static IStateConfiguration Idle => ConfigureState()
    .OnStart(t => t.Target(State.Running));
```

### `NSS004` — Trigger method must be partial void

```
[StateTriggerDefinition] method 'Open' must be declared as 'partial void' ...
```

Even on async machines, you declare triggers as `static partial void`. The generator chooses the runtime return type (`void` or `ValueTask`) based on the machine's `Async` flag; writing the return type yourself desynchronizes the declaration from what the generator needs to emit.

```csharp
// Right, for both sync and async machines
[StateTriggerDefinition] static partial void Open(string reason);
```

### `NSS005` — `[SubStateMachine]` class must be a nested partial class

```
[SubStateMachine] class 'ConnectedRegion' must be declared as a partial class nested inside
a [StateMachineDefinition] class (directly or inside another [SubStateMachine] class)
```

A region is only valid when:

- It is `partial`.
- It is **nested** inside either the root `[StateMachineDefinition]` class or another `[SubStateMachine]` class.

Free-standing regions or non-partial ones cannot participate in the generated registration.

### `NSS006` — Containing type must be partial

```
State machine class 'DoorMachine' is nested inside non-partial type 'Sample'
```

When you nest a `[StateMachineDefinition]` class inside another type (for organization, not hierarchy), every enclosing type must also be `partial`, because the generator has to re-open them to emit the state machine body.

```csharp
// Wrong
public static class Sample
{
    [StateMachineDefinition(typeof(Ctx))]
    public partial class DoorMachine { ... }
}

// Right
public static partial class Sample
{
    [StateMachineDefinition(typeof(Ctx))]
    public partial class DoorMachine { ... }
}
```

### `NSS007` — `[SubStateMachine]` parent must come from the enclosing region

```
[SubStateMachine] on 'AuthenticatedRegion' declares Parent 'Idle' which is not a state
defined in the immediately enclosing region
```

The `parent` argument must be a `[StateDefinition]` declared in the **immediately enclosing** scope (root class or outer region). Pointing at an unrelated state, a leaf deeper in the tree, or a sibling region's state breaks strict nesting.

```csharp
// Wrong: Browsing lives inside ConnectedRegion, not at the root
[SubStateMachine(parent: State.Browsing, initial: State.Foo)]
private partial class BadRegion { ... }
```

### `NSS008` — `[SubStateMachine]` initial must be defined inside the region

```
[SubStateMachine] on 'ConnectedRegion' declares Initial 'Idle' which is not a [StateDefinition]
declared inside the region
```

The `initial` argument must name a `[StateDefinition]` declared **inside** the same region class. That is the state the runtime enters when the composite is targeted.

```csharp
[SubStateMachine(parent: State.Connected, initial: State.Authenticating)]
private partial class ConnectedRegion
{
    [StateDefinition] // <-- Initial must point here or to another [StateDefinition] below
    private static IStateConfiguration Authenticating => ConfigureState()...;
}
```

### `NSS009` — Triggers cannot be declared inside a `[SubStateMachine]`

```
[StateTriggerDefinition] 'Authenticate' is declared inside [SubStateMachine] 'ConnectedRegion';
triggers must live on the root [StateMachineDefinition] class
```

Regions describe structure, not new inputs. Move any `[StateTriggerDefinition]` up to the root machine; every region already sees the root's `Trigger` enum and can react to any of them.

### `NSS010` — Async machine trigger names must not end with `Async`

```
[StateTriggerDefinition] 'InspectAsync' ends with 'Async'; on an async state machine the generator
appends the 'Async' suffix automatically - declare the trigger without the suffix
```

On async machines (`[StateMachineDefinition(typeof(Ctx), Async = true)]`) the generator appends `Async` to the Actor method name automatically (for example `Inspect` becomes `actor.InspectAsync()`). Declaring the trigger with that suffix already applied would produce a `InspectAsyncAsync` method, so the generator refuses it.

```csharp
// Wrong
[StateMachineDefinition(typeof(Ctx), Async = true)]
public partial class Scanner
{
    [StateTriggerDefinition] static partial void InspectAsync();
}

// Right — declare without the suffix, the Actor method is InspectAsync()
[StateMachineDefinition(typeof(Ctx), Async = true)]
public partial class Scanner
{
    [StateTriggerDefinition] static partial void Inspect();
}
```

The `Trigger.Inspect` enum value and the `OnInspect(...)` configurator method keep their unsuffixed names — the suffix only applies to the generated Actor method.

## Runtime exceptions

On top of compile-time diagnostics, a few exceptions surface misconfigurations that cannot be caught by the generator (for example, when you hand-build a `StateMachineDefinition` for testing).

| Exception | When | How to fix |
|-----------|------|-----------|
| `NotSupportedException: Trigger 'X' is not handled from state 'Y'` | A trigger fires with no matching transition on the leaf nor on any ancestor, **and** `OnUnhandled` is the default. | Either configure the transition, set `OnUnhandled` to a custom handler, or assign `OnUnhandled = null` to silence it. See [Unhandled triggers](sharpstate.md#unhandled-triggers). |
| `KeyNotFoundException: State 'X' is not registered in the state machine definition` | You passed a `State` to `CreateActor` that does not appear in the definition. | Use a value from the generated `State` enum; do not cast arbitrary integers to `State`. |
| `ArgumentNullException` on `CreateActor` | `context` or the internal `definition` was `null`. | Pass a non-null context. The definition is injected by the generator and is never null in practice. |
| `InvalidOperationException: State 'X' declares parent 'Y' but 'Y' does not declare an initial child via [SubStateMachine(parent: Y, initial: ...)]` | A runtime-built definition has a dangling parent reference. | For generated definitions this is enforced at compile time; for hand-built ones, always pair a parent with a region that sets `initial`. |

## Common pitfalls

- **Forgetting to declare a trigger on an ancestor that should handle it.** If `Connected` is the place where `Disconnect` should live, don't redeclare it on every child — let the hierarchy inherit it (see [Hierarchical State Machines](sharpstate-hierarchy.md#2-transitions-are-inherited-from-ancestors)).
- **Entering a composite and expecting `CurrentState` to equal the composite.** It never does — `CurrentState` is always the leaf. Use `IsIn(composite)` for the ancestor check.
- **Throwing from an action.** The exception propagates out of `Fire`/`FireAsync` and the state is **not** updated. Wrap the call in a `try`/`catch` if you want to recover, or keep actions side-effect free.
- **Mixing `Invoke` and `InvokeAsync`.** Each machine is either sync or async — set the `Async` flag on `[StateMachineDefinition]` and use the matching method. See [Asynchronous Machines](sharpstate-async.md).
- **Expecting `StateChanged` to fire for `.Stay()` transitions.** Internal transitions intentionally do not raise the event; only leaf-changing transitions do.
