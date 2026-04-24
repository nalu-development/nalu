# Post-Transition Reactions

`Nalu.SharpState` now keeps the generated actor surface synchronous: every trigger method returns `void` and dispatch happens through `Fire(...)`. When you need asynchronous follow-up work, use `ReactAsync(...)` on the trigger builder.

## What `ReactAsync(...)` does

`ReactAsync(...)` schedules fire-and-forget work **after** the transition is already finished:

```csharp
[StateMachineDefinition(typeof(InspectContext))]
public partial class ReviewMachine
{
    [StateTriggerDefinition] static partial void RequestApproval(string id);
    [StateTriggerDefinition] static partial void Approve();
    [StateTriggerDefinition] static partial void Reject();

    [StateDefinition]
    private static IStateConfiguration Pending => ConfigureState()
        .OnRequestApproval(t => t
            .Target(State.Approving)
            .ReactAsync(async (actor, ctx, id) =>
            {
                try {
                    await ctx.ApproveService.ApproveAsync(id);
                    actor.Approve();
                } catch {
                    actor.Reject();
                }
            }));

    [StateDefinition]
    private static IStateConfiguration Approving => ConfigureState()
        .OnApprove(t => t.Target(State.Approved))
        .OnReject(t => t.Target(State.Rejected));

    [StateDefinition]
    private static IStateConfiguration Approved => ConfigureState();

    [StateDefinition]
    private static IStateConfiguration Rejected => ConfigureState();
}
```

For external transitions, the execution order is:

1. `WhenExiting(...)`
2. `Invoke(...)`
3. state commit
4. `WhenEntering(...)`
5. `StateChanged`
6. `ReactAsync(...)`

For internal transitions (`Stay()` / `Ignore()`), only the inline `Invoke(...)` runs before the background reaction is scheduled.

If you use a dynamic `Target((ctx, args...) => ...)` and it resolves to the current leaf for a specific fire, that fire also behaves like an internal transition.

## Synchronization context behavior

`ReactAsync(...)` captures the current `SynchronizationContext` when the trigger is fired.

- If a context exists (for example a UI thread), the reaction starts there.
- If no context exists, the reaction is queued on the thread pool.

This keeps the main trigger path synchronous while still giving UI applications predictable follow-up scheduling. The callback also receives the generated `IActor` instance, so you can trigger additional state changes after the awaited work completes.

If `ReactAsync(...)` is registered multiple times on the same transition, the callbacks are awaited sequentially in declaration order.

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
