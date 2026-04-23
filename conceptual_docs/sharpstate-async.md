# Post-Transition Reactions

`Nalu.SharpState` now keeps the generated actor surface synchronous: every trigger method returns `void` and dispatch happens through `Fire(...)`. When you need asynchronous follow-up work, use `ReactAsync(...)` on the trigger builder.

## What `ReactAsync(...)` does

`ReactAsync(...)` schedules fire-and-forget work **after** the transition is already finished:

```csharp
[StateMachineDefinition(typeof(InspectContext))]
public partial class ReviewMachine
{
    [StateTriggerDefinition] static partial void Approve(string id);

    [StateDefinition]
    private static IStateConfiguration Pending => ConfigureState()
        .OnApprove(t => t
            .Target(State.Done)
            .Invoke((ctx, id) => ctx.LastApprovedId = id)
            .ReactAsync(async (ctx, id) =>
            {
                await ctx.Analytics.TrackApprovalAsync(id);
            }));
}
```

For external transitions, the execution order is:

1. `OnExit(...)`
2. `Invoke(...)`
3. state commit
4. `OnEntry(...)`
5. `StateChanged`
6. `ReactAsync(...)`

For internal transitions (`Stay()` / `Ignore()`), only the inline `Invoke(...)` runs before the background reaction is scheduled.

## Synchronization context behavior

`ReactAsync(...)` captures the current `SynchronizationContext` when the trigger is fired.

- If a context exists (for example a UI thread), the reaction starts there.
- If no context exists, the reaction is queued on the thread pool.

This keeps the main trigger path synchronous while still giving UI applications predictable follow-up scheduling.

## Failure reporting

Because the reaction is fire-and-forget, exceptions do **not** flow back out of the trigger method. Instead, subscribe to `ReactionFailed`:

```csharp
actor.ReactionFailed += (from, to, trigger, args, exception) =>
    logger.LogError(exception, "Reaction failed for {Trigger}", trigger);
```

The event is raised with:

- the committed source leaf state
- the committed destination leaf state
- the trigger
- the boxed trigger arguments
- the thrown exception

## When to use `Invoke(...)` vs `ReactAsync(...)`

Use `Invoke(...)` when the side effect is part of the transition itself and must complete before the new state becomes visible.

Use `ReactAsync(...)` when the transition should commit immediately and the asynchronous work is a follow-up concern such as telemetry, notifications, cache refreshes, or best-effort synchronization.
