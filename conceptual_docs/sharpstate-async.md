# Asynchronous Machines

By default, `Nalu.SharpState` generates **synchronous** actors: every trigger method returns `void` and actions run inline. Set `Async = true` on the machine attribute and the whole surface flips to `ValueTask`-based APIs, with `InvokeAsync` replacing `Invoke` on the fluent builder.

> **Naming rule**: trigger methods on the generated Actor receive an `Async` suffix (for example `Inspect` → `InspectAsync`) when the machine is async. The trigger declaration itself and the `On<Name>` configurator method stay without the suffix. If you declare a trigger whose name already ends with `Async` on an async machine, the generator reports `NSS010`.

## Opting in

```csharp
[StateMachineDefinition(typeof(InspectContext), Async = true)]
public partial class AsyncMachine
{
    [StateTriggerDefinition] static partial void Inspect();
    [StateTriggerDefinition] static partial void Finish();

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState()
        .OnInspect(t => t
            .Stay()
            .InvokeAsync(async ctx =>
            {
                await Task.Delay(10);
                ctx.Inspections++;
            }))
        .OnFinish(t => t.Target(State.Done));

    [StateDefinition]
    private static IStateConfiguration Done => ConfigureState();
}
```

What changes compared to a sync machine:

| Aspect | Sync (default) | Async (`Async = true`) |
|--------|----------------|------------------------|
| Trigger method signature | `Inspect(args...)` (`void`) | `InspectAsync(args...)` (`ValueTask`) |
| Fluent builder interface | `ISyncStateTriggerBuilder<...>` | `IAsyncStateTriggerBuilder<...>` |
| Available action method | `Invoke(Action<...>)` | `InvokeAsync(Func<..., ValueTask>)` |
| Dispatch path | `engine.Fire(...)` | `await engine.FireAsync(...)` |

The trigger signature you declare with `[StateTriggerDefinition]` stays the same — always `partial void Name(args)`. Only the generated method on the `Actor` changes: it gains the `Async` suffix and returns `ValueTask`. `Trigger.Inspect` and `OnInspect(...)` keep their unsuffixed names.

## Calling the actor

```csharp
var actor = AsyncMachine.CreateActor(AsyncMachine.State.Idle, new InspectContext());

// Triggers now return ValueTask and carry an Async suffix
ValueTask pending = actor.InspectAsync();
await pending;

await actor.FinishAsync();
Console.WriteLine(actor.CurrentState); // Done
```

Guard evaluation is still synchronous. Only the `InvokeAsync` action is awaited. The state commits **after** the action completes, so you can observe the new state in any code that `await`s the trigger.

## Sync vs async: pick one

The builder interfaces for sync and async machines are **distinct types**, which means:

- On a sync machine, the builder has only `Invoke(...)` — calling `InvokeAsync` would not compile.
- On an async machine, the builder has only `InvokeAsync(...)` — calling `Invoke` would not compile.

This separation keeps the two worlds from leaking into each other. If you need async work inside a sync machine you have two options:

1. **Fire-and-forget the work yourself** inside `Invoke`, accepting that the state will commit immediately regardless of how the async work ends.
2. **Convert the whole machine to async** by setting `Async = true`. This is usually the right choice when at least one transition needs to await I/O.

## Actions, guards, and context

`InvokeAsync` receives the context followed by the trigger's typed parameters:

```csharp
[StateTriggerDefinition] static partial void Save(Document doc);

[StateDefinition]
private static IStateConfiguration Editing => ConfigureState()
    .OnSave(t => t
        .When((ctx, doc) => doc.IsValid)
        .Target(State.Saved)
        .InvokeAsync(async (ctx, doc) =>
        {
            await ctx.Repository.WriteAsync(doc);
            ctx.LastSavedAt = DateTimeOffset.UtcNow;
        }));
```

Guards remain synchronous predicates — don't do I/O there. If a guard needs async data, compute it beforehand and cache it on the context, then let the guard read the cached value.

## Error handling

If the async action throws, the exception propagates out of `FireAsync` and **no state change occurs** (because the commit happens after the action completes). `StateChanged` is not raised. `OnUnhandled` is not raised either — this only fires when there is no matching transition, not when an action throws.

```csharp
try
{
    await actor.SaveAsync(document);
}
catch (HttpRequestException)
{
    // state is unchanged, actor still in the pre-trigger state
}
```

This keeps your state machine consistent with failures: a failed side effect never silently moves you to a new state.

## Cancellation

Cancellation is **not** plumbed through the generated methods — keep your `CancellationToken` on the context or pass it as a regular trigger argument:

```csharp
[StateTriggerDefinition] static partial void Save(Document doc, CancellationToken ct);

[StateDefinition]
private static IStateConfiguration Editing => ConfigureState()
    .OnSave(t => t
        .Target(State.Saved)
        .InvokeAsync((ctx, doc, ct) => ctx.Repository.WriteAsync(doc, ct)));
```

If the token is canceled inside the action, the resulting `OperationCanceledException` follows the same rules as any other exception: state stays put and the exception bubbles out of `FireAsync`.
