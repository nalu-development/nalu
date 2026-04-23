# Nalu.SharpState

[![Nalu.SharpState NuGet Package](https://img.shields.io/nuget/v/Nalu.SharpState.svg)](https://www.nuget.org/packages/Nalu.SharpState/) [![Nalu.SharpState NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.SharpState)](https://www.nuget.org/packages/Nalu.SharpState/)

A compile-time, AOT-friendly state machine for .NET built on a Roslyn source generator. You declare states and triggers with attributes, describe transitions with a strongly-typed fluent API, and the generator emits a ready-to-use `Actor` class with typed trigger methods.

## Why SharpState?

Classic state machine libraries rely on reflection, dictionaries keyed by strings/enums at runtime, and `object[]` parameter bags. That costs boxing, breaks AOT, and pushes errors from compile time to the first user interaction.

`Nalu.SharpState` takes the opposite route:

- **Declarative**: states and triggers are C# constructs (a `static partial` method is a trigger, a `static` property is a state).
- **Strongly typed**: trigger parameters become method parameters on the generated actor. Guards and actions see the exact types you declared.
- **Compile-time validated**: duplicate names, unreachable hierarchies, and misconfigured sub-machines become build errors via dedicated `NSS001`-`NSS009` diagnostics.
- **AOT / trim friendly**: zero reflection at runtime. The generator emits the registration tables at compile time.
- **Hierarchical**: composite states are modeled as nested `[SubStateMachine]` partial classes with strict scoping rules.
- **Sync-first**: generated actors stay synchronous, with optional fire-and-forget `ReactAsync(...)` callbacks for post-transition work.

## Installation

```bash
dotnet add package Nalu.SharpState
```

The package bundles the source generator, so no additional setup or `UseXxx(...)` call is required.

## Anatomy of a machine

A machine lives in a single `partial class` marked with `[StateMachineDefinition]`. It is made of three building blocks:

| Building block | Declared as | Role |
|----------------|-------------|------|
| **Context** | Any class you own | Carries data into every guard and action. Passed as a type argument to `[StateMachineDefinition]`. |
| **Triggers** | `[StateTriggerDefinition] static partial void` methods | Inputs to the machine. Their parameter list becomes the dispatch signature. |
| **States** | `[StateDefinition] static IStateConfiguration` properties | Nodes of the machine. The property body configures outgoing transitions. |

Here is a minimal door:

```csharp
using Nalu.SharpState;

public class DoorContext
{
    public int OpenCount { get; set; }
    public string? LastReason { get; set; }
}

[StateMachineDefinition(typeof(DoorContext))]
public partial class DoorMachine
{
    [StateTriggerDefinition] static partial void Open(string reason);
    [StateTriggerDefinition] static partial void Close();

    [StateDefinition]
    private static IStateConfiguration Closed => ConfigureState()
        .OnOpen(t => t
            .Target(State.Opened)
            .Invoke((ctx, reason) =>
            {
                ctx.OpenCount++;
                ctx.LastReason = reason;
            }));

    [StateDefinition]
    private static IStateConfiguration Opened => ConfigureState()
        .OnClose(t => t.Target(State.Closed));
}
```

The generator produces:

- A `State` enum with the values `Closed, Opened`.
- A `Trigger` enum with the values `Open, Close`.
- A nested `public sealed class Actor` exposing `Open(string)`, `Close()`, `CurrentState`, `Context`, `IsIn(...)`, `StateChanged`, and `OnUnhandled`.
- A `public static Actor CreateActor(State currentState, DoorContext context)` factory.

Usage:

```csharp
var door = DoorMachine.CreateActor(DoorMachine.State.Closed, new DoorContext());

door.Open("delivery");

Console.WriteLine(door.CurrentState);     // Opened
Console.WriteLine(door.Context.OpenCount); // 1
```

## Describing transitions

Inside each `[StateDefinition]` property body you call `ConfigureState()` and chain one `On<TriggerName>(t => ...)` per trigger the state reacts to. The builder `t` exposes four methods:

| Method | Purpose |
|--------|---------|
| `Target(State s)` | Move to `s` when the trigger fires. If `s` is composite, its initial-child chain is resolved to a leaf. |
| `Stay()` | Run the action but keep the current state (internal transition). `StateChanged` is **not** raised. |
| `Ignore()` | Syntax sugar for `Stay()` with no action, useful when a trigger should be accepted but do nothing. |
| `When(predicate)` | Guard the transition. The predicate receives the context and trigger arguments. |
| `Invoke(action)` | Run side effects before the state commits. The signature mirrors the trigger's parameters. |
| `ReactAsync(action)` | Schedule fire-and-forget work after the transition commits and after `StateChanged` fires. |

You can register multiple transitions for the same trigger in the same state; the first one whose guard passes (or has no guard) wins:

```csharp
[StateDefinition]
private static IStateConfiguration Idle => ConfigureState()
    .OnStart(t => t
        .When((ctx, user) => user.IsAdmin)
        .Target(State.AdminDashboard))
    .OnStart(t => t
        .Target(State.UserDashboard));
```

If no transition matches, the `OnUnhandled` callback fires (see below).

### Entry and exit hooks

States can also react to external transitions with `WhenEntering(...)` and `WhenExiting(...)`:

```csharp
[StateDefinition]
private static IStateConfiguration Running => ConfigureState()
    .WhenEntering(ctx => ctx.Log.Add("entered running"))
    .WhenExiting(ctx => ctx.Log.Add("leaving running"))
    .OnStop(t => t.Target(State.Stopped));
```

Hooks run only for external transitions:

- Exit hooks run from the current leaf upward until the lowest common ancestor with the destination.
- Entry hooks run from that ancestor's child down to the new leaf.
- Internal transitions (`Stay()` / `Ignore()`) do not fire entry or exit hooks.

## Interacting with the actor

The generated `Actor` exposes everything you need at runtime:

```csharp
var door = DoorMachine.CreateActor(DoorMachine.State.Closed, new DoorContext());

door.StateChanged += (from, to, trigger, args) =>
    Console.WriteLine($"{from} -> {to} via {trigger}");

door.OnUnhandled = (current, trigger, args) =>
    logger.LogWarning("{Trigger} ignored while in {State}", trigger, current);

door.Open("delivery");
Console.WriteLine(door.CurrentState);         // Opened
Console.WriteLine(door.IsIn(DoorMachine.State.Opened)); // true
```

| Member | Description |
|--------|-------------|
| `CurrentState` | Current **leaf** state. When the machine is in a nested composite it always reports the leaf. |
| `Context` | The context instance supplied to `CreateActor`. |
| `IsIn(State s)` | `true` if `CurrentState` equals `s` **or** is a descendant of `s`. Use this to query composites. |
| `StateChanged` | `StateChangedHandler<State, Trigger>` raised **after** a non-internal transition commits. |
| `ReactionFailed` | Raised when a background `ReactAsync(...)` callback throws after the transition already completed. |
| `OnUnhandled` | Invoked when a trigger has no matching transition on the leaf nor on any ancestor. |
| `<Trigger>(...)` | One strongly-typed `void` method per trigger. |

### Unhandled triggers

`OnUnhandled` defaults to a handler that throws `NotSupportedException` with the current state and trigger in the message. This surfaces programming mistakes early (e.g. firing `Close` while the door is already closed and no `.OnClose` is configured for that state).

You have three options:

```csharp
// 1) Default: throws on unhandled
door.Open("again");  // NotSupportedException if not configured

// 2) Custom handler (logging, metrics, retries...)
door.OnUnhandled = (state, trigger, args) =>
    telemetry.Track("UnhandledTrigger", state, trigger);

// 3) Opt out: silent no-op
door.OnUnhandled = null;
```

If a trigger should be accepted silently from a given state, prefer modeling that directly:

```csharp
[StateDefinition]
private static IStateConfiguration Running => ConfigureState()
    .OnHeartbeat(t => t.Ignore());
```

### StateChanged

`StateChanged` fires once per committed transition, with the original leaf, the new leaf, the trigger, and the boxed arguments.

```csharp
door.StateChanged += (from, to, trigger, args) => { /* log, react, ... */ };
```

It is **not** raised when:

- The transition is internal (`.Stay()`).
- The trigger was unhandled (`OnUnhandled` is raised instead).

## Guards and actions

Guards and actions both receive the context followed by the trigger's parameters — exactly the parameters you declared on the `partial void` method:

```csharp
[StateTriggerDefinition] static partial void Withdraw(decimal amount, string note);

[StateDefinition]
private static IStateConfiguration Open => ConfigureState()
    .OnWithdraw(t => t
        .When((ctx, amount, _) => amount <= ctx.Balance)
        .Target(State.Open)
        .Invoke((ctx, amount, note) =>
        {
            ctx.Balance -= amount;
            ctx.Ledger.Add(($"-{amount}", note));
        }))
    .OnWithdraw(t => t
        .Stay()
        .Invoke((ctx, amount, note) =>
            ctx.Ledger.Add((note, $"insufficient for {amount}"))));
```

Guards are pure predicates — keep them free of side effects. `Invoke(...)` runs inline during dispatch and any exception it throws propagates out of `Fire(...)` before the new state is committed.

### ReactAsync

Use `ReactAsync(...)` when the transition should commit immediately but you still want to kick off asynchronous follow-up work:

```csharp
[StateDefinition]
private static IStateConfiguration Saving => ConfigureState()
    .OnSaved(t => t
        .Target(State.Ready)
        .Invoke(ctx => ctx.LastSavedAt = DateTimeOffset.UtcNow)
        .ReactAsync(async (actor, ctx) =>
        {
            await ctx.Analytics.TrackSavedAsync();
            // actor.PublishCompleted();
        }));
```

For external transitions, the order is:

1. `WhenExiting(...)`
2. `Invoke(...)`
3. state commit
4. `WhenEntering(...)`
5. `StateChanged`
6. `ReactAsync(...)`

`ReactAsync(...)` captures the current `SynchronizationContext` when the trigger is fired. The callback receives the generated actor instance first, so it can trigger additional transitions after the awaited work completes. If the background reaction throws, the exception is surfaced through `ReactionFailed`.

## What next?

- [Benchmarks](../Tests/Nalu.SharpState.Benchmarks/) — measure trigger dispatch and allocations on your machine.
- [Hierarchical State Machines](sharpstate-hierarchy.md) — nested composite states via `[SubStateMachine]`.
- [Post-Transition Reactions](sharpstate-async.md) — background `ReactAsync(...)` work and `ReactionFailed`.
- [Diagnostics & Troubleshooting](sharpstate-diagnostics.md) — generator errors (`NSS001`-`NSS009`) and common pitfalls.
