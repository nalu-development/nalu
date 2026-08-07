# Navigation State Restoration

Nalu can snapshot your **navigation state** and land the user exactly where they were after an
app restart: the selected root, the pushed stack, and the intents that materialized those
pages. Restore replays *navigation* through the normal pipeline — it never serializes page UI
state.

Typical uses:

- **Developer experience** (the reason to turn it on today): restart the app during
  development and keep your place, however deep in the navigation you were.
- **Android process death** (production): the same mechanism, enabled deliberately.

> Restore is an engine-level feature, currently verified with the
> [Scaffold](scaffold.md) host.

## Enabling it

Restore is opt-in per app:

```csharp
.UseNaluNavigation<App>(nav => nav
    .AddPages()
    .WithRestore(restore =>
    {
#if !DEBUG
        restore.Enabled = false;            // DEBUG-only DevEx policy, expressed app-side
#endif
        restore.MaxAge = TimeSpan.FromHours(12);   // optional: stale snapshots are discarded
    })
)
```

`Enabled` defaults to `true` once `WithRestore` is called — the library cannot see your build
configuration, so a DEBUG-only policy is a one-line app-side decision (as above).

## Capture is automatic

Every successful navigation re-captures the state, and each page's **entering intent** is
recorded (and serialized) at navigation time. Whether a page is restorable derives from how it
was reached:

- navigated to **without an intent** → restorable (it needs no context to reproduce);
- navigated to with an intent that **serializes** → restorable, the same intent replays on
  restore;
- navigated to with an intent that **cannot serialize** (or whose type no longer resolves at
  restore time — a rename) → the restorable stack *ends at that page*: its context cannot be
  reproduced, so neither it nor anything above it restores.

Root selection (and the root's own intent, when a root switch carried one) is captured the
same way. Intents are plain objects, serialized as-is (JSON by default) at navigation time —
no marker interface, no registration, no live-object retention; the type's full name is the
wire identifier. Pop intents are appearing context, not entering context: they never replace
what recreates a page.

### Non-serializable intent state: `[JsonIgnore]` + hydration

An intent may carry state that cannot (or should not) serialize — a live domain object, a
stream, a service reference. Exclude it with `[JsonIgnore]` and rehydrate it on restore:

```csharp
public class DocumentIntent
{
    public required string DocumentId { get; set; }

    [JsonIgnore]                                   // not persisted: rehydrated on restore
    public Document? Document { get; set; }
}

public class HomePageModel(IDocumentRepository repository)
    : IIntentHydrator<DocumentIntent>
{
    public async ValueTask HydrateAsync(DocumentIntent intent)
        => intent.Document = await repository.LoadAsync(intent.DocumentId);
}
```

During the replay, BEFORE navigating with a restored intent, the engine walks the
already-restored stack from the top page down to the root and awaits the first page (model)
implementing `IIntentHydrator<TIntent>` for that intent type — so the missing state is filled
by a page that is already alive (the initialization root qualifies too), and the navigation
then delivers the finalized intent through the normal pipeline.

Snapshots are written debounced in the background (never affecting navigation) and **flushed
immediately when the app is backgrounded** — the last reliable moment before a potential
process death.

## The `INavigationRestore` service

Inject `INavigationRestore` (always available; inert when restore is not enabled) for the
three explicit controls. The per-page methods deduce the page they act on — the page whose
lifecycle callback is running, or the current page otherwise:

```csharp
public class CheckoutPageModel(INavigationRestore restore) : IEnteringAware
{
    public ValueTask OnEnteringAsync()
        // Wizard flows must never resurrect: the restorable stack ends here.
        => new(restore.ForgetAsync());
}
```

- **`ForgetAsync()`** — removes the current page from the restoration stack: a restore lands
  on the page below it (and pages above cannot restore either). Lasts until the page pops.
- **`RestoreWithIntentAsync(intent)`** — sets or replaces the intent replayed for the current
  page: swap a "create draft" intent for a "saved entity id" one once state materializes, or
  make a page reached with an unserializable intent restorable again by providing a
  serializable equivalent of its context (serialization failures throw at this call site).
- **`TryStopRestoreAsync()`** — see below.

Both per-page methods persist the updated snapshot before their task completes.

## What happens at boot

1. The snapshot is read **and deleted** (a replay that crashes yields a clean next boot),
   then validated: schema version, app version/build, a hash of the restorable route table
   (roots and registered pages), and `MaxAge`. Any mismatch discards it.
2. The engine boots **your configured initial destination as normal** — an app's
   initialization root always runs first, doing whatever essential work it does.
3. When the initial page's first `OnAppearingAsync` completes, the replay executes: one
   navigation selecting the captured root (with its intent), then the captured pushes —
   chunked so every captured intent rides its own navigation's target through the normal
   pipeline. Animations, lifecycle and intent delivery are exactly the live ones.
4. The snapshot is re-persisted (capture is automatic, so the replay itself re-recorded it).

Everything is **fail-open**: an unknown page segment or an unresolvable intent type truncates
the restored stack at that frame; any error discards the snapshot and boots the default
destination. Restore can never
brick startup. It also runs **once per app launch** — a host created later in the same process
(logout/login swap) boots normally.

### The suppression window and `TryStopRestoreAsync`

While a restore is pending, navigations **not issued by the replay are ignored** (they
return `false` and raise the `NavigationIgnored` lifecycle event). This is deterministic, not
a race: each replay step is enqueued through the dispatcher, so an auto-navigation a restored
page fires from its lifecycle (the usual `DispatchAsync` pattern) always drains *before* the
next replay step — inside the window. The window lifts just before the **last** restored
destination, so the page the user actually was on keeps its right to auto-navigate.

The escape hatch is for flows that must legitimately win — authentication above all:

```csharp
public async Task OnInitializationCompletedAsync()
{
    if (!user.IsAuthenticated)
    {
        // This restore shall not happen: drop it, lift the suppression, navigate freely.
        await restore.TryStopRestoreAsync();
        await navigation.GoToAsync(Navigation.Absolute().Root<LoginPageModel>());
        return;
    }

    // Authenticated and a restore is pending: do nothing — the replay lands the user back.
}
```

`TryStopRestoreAsync()` returns `true` when there was a pending (or in-flight) restore to
stop; the discarded snapshot is replaced by a fresh capture of wherever the app goes next.

## Customization

- **`IIntentSerializer`** — the wire format. The default is System.Text.Json reflection; for
  trimmed/NativeAOT apps supply a source-generated context via
  `restore.IntentSerializerContext = MyJsonContext.Default` (every intent type the app
  navigates with must be included), or replace the service in DI entirely.
- **`INavigationRestoreStore`** — persistence. The default is a JSON file in the app cache
  directory (which has exactly the "safe to delete" semantics restore data wants); replace it
  in DI for custom locations.
- **Intent type identity** — the snapshot stores the intent type's namespace-qualified full
  name (deliberately NOT assembly-qualified): a renamed or removed type simply fails to
  resolve at restore time and truncates the restored stack fail-open.

## What does NOT restore

- **View state** (scroll offsets, entry text): restore replays navigation; page state is the
  page's concern.
- **Forgotten pages** (`ForgetAsync`), pages reached with unserializable intents, and
  anything above them in the stack.
- **Non-current roots' stacks**: the current root/stack only — other tabs restart fresh.
- **Transient overlays** (popups/sheets/flyouts). Modal pages restore as part of the stack —
  their presentation mode re-resolves from the recreated page.
