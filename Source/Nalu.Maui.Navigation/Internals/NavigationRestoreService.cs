using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Nalu;

/// <summary>
/// The <see cref="INavigationRestore"/> implementation: automatic capture of the navigation
/// state (with per-page entering intents recorded at navigation time), boot-time replay after
/// the initial page's first appearing, and the pending-restore navigation-suppression window.
/// Always registered; INERT unless <c>UseNaluNavigationRestore(...)</c> enabled it.
/// </summary>
/// <remarks>
/// Threading: every capture trigger (engine hooks, the per-page methods) runs on the UI
/// thread, where the navigation state lives — capture is synchronous and cheap; only the
/// store write runs debounced in the background, fire-and-forget-swallowed (capture must
/// never affect navigation). The per-page methods flush before completing.
/// </remarks>
internal sealed class NavigationRestoreService : INavigationRestore, IDisposable
{
    private const int _schemaVersion = 1;
    private static readonly TimeSpan _writeDebounce = TimeSpan.FromMilliseconds(500);

    /// <summary>Restore-relevant state of a live page, keyed weakly by the page itself.</summary>
    private sealed class PageRestoreState
    {
        /// <summary>The restorable stack ends at this page (ForgetAsync, or a non-serializable intent).</summary>
        public bool NonRestorable { get; set; }

        /// <summary>The serialized intent replayed for this page, when it has one.</summary>
        public NavigationRestoreIntentData? Intent { get; set; }
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly NavigationRestoreOptions? _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NavigationRestoreService>? _logger;
    private readonly ConditionalWeakTable<Page, PageRestoreState> _pageStates = [];
    private readonly HashSet<Window> _hookedWindows = [];
    private readonly AsyncLocal<bool> _isReplayNavigation = new();

    private NavigationRestoreBoot? _pendingBoot;
    private bool _bootAttempted;
    private bool _suppressNavigations;
    private bool _stopRequested;
    private string? _pendingPayload;
    private CancellationTokenSource? _writeDebounceCts;

    /// <summary>Test hook: the app identity stamped into (and validated against) the snapshot header.</summary>
    internal Func<string> AppVersionProvider { get; set; } = GetDefaultAppVersion;

    internal bool IsEnabled => _options?.Enabled == true;

    /// <summary>A validated snapshot is pending or replaying: non-replay navigations are ignored.</summary>
    internal bool ShouldIgnoreNavigation => _suppressNavigations && !_isReplayNavigation.Value;

    /// <summary>Test seam: whether the suppression window is currently armed (regardless of the replay flow).</summary>
    internal bool IsSuppressionActive => _suppressNavigations;

    private NavigationService NavigationService => field ??= (NavigationService) _serviceProvider.GetRequiredService<INavigationService>();

    private IIntentSerializer IntentSerializer => field ??= _serviceProvider.GetRequiredService<IIntentSerializer>();

    private INavigationRestoreStore Store => field ??= _serviceProvider.GetRequiredService<INavigationRestoreStore>();

    public NavigationRestoreService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _options = serviceProvider.GetService<NavigationRestoreOptions>();
        _timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        _logger = serviceProvider.GetService<ILogger<NavigationRestoreService>>();
    }

    #region INavigationRestore

    public Task ForgetAsync()
    {
        if (!IsEnabled || ResolveCurrentPage() is not { } page)
        {
            return Task.CompletedTask;
        }

        GetState(page).NonRestorable = true;

        return CaptureAndFlushAsync();
    }

    public Task RestoreWithIntentAsync(object intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!IsEnabled)
        {
            return Task.CompletedTask;
        }

        var intentType = intent.GetType();

        if (!_options!.IntentIdsByType.TryGetValue(intentType, out var typeId))
        {
            // A deterministic misconfiguration, not a transient failure: always throw.
            throw new InvalidOperationException(
                $"Intent type {intentType.FullName} is not registered for restore; register it via UseNaluNavigationRestore(r => r.AddIntent<{intentType.Name}>())."
            );
        }

        // Serialized NOW: no live-object retention, failures throw at the call site.
        var payload = IntentSerializer.Serialize(intent);

        if (ResolveCurrentPage() is not { } page)
        {
            return Task.CompletedTask;
        }

        var state = GetState(page);
        state.NonRestorable = false;
        state.Intent = new NavigationRestoreIntentData { TypeId = typeId, Payload = payload };

        return CaptureAndFlushAsync();
    }

    public async Task<bool> TryStopRestoreAsync()
    {
        if (!_suppressNavigations)
        {
            return false;
        }

        // Stops the in-flight replay after its current navigation, drops the pending boot and
        // lifts the suppression window: the caller can navigate freely right away.
        _stopRequested = true;
        _pendingBoot = null;
        _suppressNavigations = false;

        // The snapshot on disk was already consumed (read-and-delete at boot); re-capture the
        // CURRENT state so the next launch reflects wherever the app actually goes next.
        await CaptureAndFlushAsync();

        return true;
    }

    /// <summary>
    /// The page the per-page methods act on: the page whose lifecycle callback is currently
    /// running (pushed pages are not on the committed stack during their entering), or the
    /// current top page otherwise (commands run while their page is current).
    /// </summary>
    private Page? ResolveCurrentPage()
    {
        if (NavigationHelper.AmbientLifecyclePage is { } lifecyclePage)
        {
            return lifecyclePage;
        }

        var stack = GetCurrentStack();

        return stack is { Count: > 0 } ? stack[^1].Page : null;
    }

    #endregion

    #region Engine hooks

    /// <summary>
    /// Boot: reads-and-DELETES the snapshot and validates it against the live structure and
    /// registrations. A valid snapshot arms the pending restore — from here until the replay
    /// completes, non-replay navigations are ignored. Fail-open on ANY problem: restore must
    /// never brick startup. Once per app launch (a later host in the same process boots
    /// normally).
    /// </summary>
    internal void OnEngineInitializing(IShellProxy shellProxy)
    {
        if (!IsEnabled || _bootAttempted)
        {
            return;
        }

        _bootAttempted = true;

        try
        {
            HookWindows();

            var payload = Store.ReadAndDelete();

            if (payload is null)
            {
                _logger?.LogDebug("No navigation-state snapshot to restore.");

                return;
            }

            var snapshot = JsonSerializer.Deserialize(payload, NavigationRestoreJsonContext.Default.NavigationRestoreSnapshot);

            // Every rejection below is silent by design (the app boots its default destination)
            // but logged: "why did it not restore?" is otherwise undiagnosable in the field.
            if (snapshot is null || snapshot.SchemaVersion != _schemaVersion)
            {
                _logger?.LogInformation("Navigation-state snapshot discarded: unreadable or schema {Schema} != {Expected}.", snapshot?.SchemaVersion, _schemaVersion);

                return;
            }

            if (snapshot.AppVersion != AppVersionProvider())
            {
                _logger?.LogInformation("Navigation-state snapshot discarded: captured by app version {Captured}, running {Current}.", snapshot.AppVersion, AppVersionProvider());

                return;
            }

            if (snapshot.RouteHash != ComputeRouteHash(shellProxy))
            {
                _logger?.LogInformation("Navigation-state snapshot discarded: the route structure (roots / registered intents) changed since capture.");

                return;
            }

            if (snapshot.RootSegment is null)
            {
                _logger?.LogInformation("Navigation-state snapshot discarded: the captured root was not restorable.");

                return;
            }

            if (_options!.MaxAge is { } maxAge && _timeProvider.GetUtcNow() - snapshot.CapturedAt > maxAge)
            {
                _logger?.LogInformation("Navigation-state snapshot discarded: older than MaxAge ({MaxAge}).", maxAge);

                return;
            }

            var typesBySegment = BuildSegmentTypeMap(shellProxy);

            if (!typesBySegment.TryGetValue(snapshot.RootSegment, out var rootPageType)
                || !GetOrderedRootSegments(shellProxy).Contains(snapshot.RootSegment, StringComparer.Ordinal))
            {
                _logger?.LogInformation("Navigation-state snapshot discarded: root segment {Segment} is not a root of the current structure.", snapshot.RootSegment);

                return;
            }

            // Any deserialization exception below propagates to the catch: the whole snapshot
            // is discarded (its context is not trustworthy anymore).
            object? rootIntent = null;

            if (snapshot.RootIntent is { } rootIntentData)
            {
                rootIntent = DeserializeIntent(rootIntentData);

                if (rootIntent is null)
                {
                    // Unknown root intent id: cannot reproduce the root's context (defensive —
                    // the route hash covers intent ids, so this should not happen).
                    return;
                }
            }

            var frames = new List<NavigationRestoreFrame>();

            foreach (var frameData in snapshot.Frames)
            {
                // Unknown segment/type id truncates the prefix at that frame (fail-open).
                if (frameData.Segment is null || !typesBySegment.TryGetValue(frameData.Segment, out var pageType))
                {
                    break;
                }

                object? intent = null;

                if (frameData.Intent is { } intentData)
                {
                    intent = DeserializeIntent(intentData);

                    if (intent is null)
                    {
                        break;
                    }
                }

                frames.Add(new NavigationRestoreFrame(frameData.Segment, pageType, intent));
            }

            _pendingBoot = new NavigationRestoreBoot
            {
                RootSegment = snapshot.RootSegment,
                RootPageType = rootPageType,
                RootIntent = rootIntent,
                Frames = frames
            };

            _logger?.LogDebug("Navigation-state snapshot accepted: root {Root}, {Frames} frame(s).", snapshot.RootSegment, frames.Count);

            _stopRequested = false;
            _suppressNavigations = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Navigation-state snapshot rejected; booting the default destination.");
            _pendingBoot = null;
            _suppressNavigations = false;
        }
    }

    /// <summary>Records the initial page's boot intent (same rules as navigated intents).</summary>
    internal void OnRootEntered(Page rootPage, object? intent)
    {
        if (IsEnabled)
        {
            RecordIntent(rootPage, intent);
        }
    }

    /// <summary>
    /// A successful navigation: records the target page's entering intent and re-captures.
    /// Pop intents are appearing context, not entering context — they never replace what
    /// recreates the revealed page, so pop-ending navigations only re-capture.
    /// </summary>
    internal void OnNavigationCompleted(INavigationInfo navigation)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (navigation.Count > 0
            && navigation[^1].SegmentName != NavigationPop.PopRoute
            && navigation.Intent is { } intent
            && GetCurrentStack() is { Count: > 0 } stack)
        {
            RecordIntent(stack[^1].Page, intent);
        }

        ScheduleCapture();
    }

    /// <summary>
    /// Replays the pending restore AFTER the initial page's first appearing completed (the
    /// app's initialization root always runs first): one navigation selecting the captured
    /// root (with its intent), then chunked pushes — each chunk ends at an intent-carrying
    /// frame, so every captured intent rides its navigation's target through the normal
    /// pipeline. Runs through the regular navigation service: animations and lifecycle are
    /// the live ones.
    /// </summary>
    /// <remarks>
    /// Every replay navigation is enqueued through the DISPATCHER: an auto-navigation a
    /// restored page fires from its lifecycle (the prescribed <c>DispatchAsync</c> pattern)
    /// is enqueued while our navigation is still executing, so it always drains BEFORE our
    /// next replay step — deterministically inside the suppression window, deterministically
    /// ignored. The suppression window is lifted INSIDE the last replay step, right before its
    /// navigation runs: whatever the app dispatched earlier (the initialization root's redirect)
    /// is ahead of that step in the queue and drains ignored, while the page the user actually
    /// was on keeps its right to auto-navigate (its dispatched redirect is queued after).
    /// </remarks>
    internal async Task ReplayPendingAsync()
    {
        if (_pendingBoot is not { } boot)
        {
            return;
        }

        _pendingBoot = null;

        try
        {
            // Root selection first: count==1 delivers the root intent to the root content.
            var rootNavigation = new AbsoluteNavigation();
            ((IList<INavigationSegment>) rootNavigation).Add(new NavigationSegment { SegmentName = boot.RootSegment, Type = boot.RootPageType });

            if (boot.RootIntent is not null)
            {
                ((IAbsoluteNavigationBuilder) rootNavigation).WithIntent(boot.RootIntent);
            }

            var navigations = new List<(INavigationInfo Navigation, object? Intent)>
            {
                (rootNavigation, boot.RootIntent)
            };

            foreach (var (chunkFrames, chunkIntent) in ChunkFrames(boot.Frames))
            {
                var navigation = new RelativeNavigation();
                IList<INavigationSegment> segments = navigation;

                foreach (var frame in chunkFrames)
                {
                    segments.Add(
                        new NavigationSegment
                        {
                            SegmentName = frame.Segment,
                            Type = frame.PageType
                        }
                    );
                }

                if (chunkIntent is not null)
                {
                    navigation.WithIntent(chunkIntent);
                }

                navigations.Add((navigation, chunkIntent));
            }

            if (GetCurrentStack() is not { Count: > 0 } bootStack)
            {
                return;
            }

            var dispatcher = bootStack[0].Page.Dispatcher;

            for (var i = 0; i < navigations.Count; i++)
            {
                if (_stopRequested)
                {
                    return;
                }

                // The LAST restored destination may auto-navigate: the window lifts right before
                // its navigation runs — INSIDE the dispatched step, not here. Anything the app
                // dispatched earlier (the initialization root's own redirect, queued during its
                // boot appearing) sits ahead of that step in the queue and therefore still drains
                // inside the window — deterministically ignored — while the final destination's
                // own dispatched redirect, queued during its navigation, runs after it, open.
                var liftSuppression = i == navigations.Count - 1;

                if (!await DispatchReplayNavigationAsync(dispatcher, navigations[i].Navigation, navigations[i].Intent, liftSuppression))
                {
                    // Canceled/failed guard-free push should not happen; keep what landed.
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Restore replay stopped by an exception; keeping the pages restored so far.");
        }
        finally
        {
            _suppressNavigations = false;

            // Re-persist immediately (not debounced): the snapshot was DELETED at boot, so
            // until this write a crash would lose the restored state.
            await SwallowedFlushAsync();
        }
    }

    /// <summary>
    /// Enqueues one replay navigation behind whatever the previously-restored pages already
    /// dispatched; the replay-bypass flag is set INSIDE the dispatched flow (AsyncLocal — it
    /// must not leak onto interleaving auto-navigations).
    /// </summary>
    private Task<bool> DispatchReplayNavigationAsync(IDispatcher dispatcher, INavigationInfo navigation, object? intent, bool liftSuppression)
        => dispatcher.DispatchAsync(async () =>
            {
                _isReplayNavigation.Value = true;

                try
                {
                    if (intent is not null)
                    {
                        await HydrateIntentAsync(intent);
                    }

                    if (liftSuppression)
                    {
                        _suppressNavigations = false;
                    }

                    return await NavigationService.GoToAsync(navigation);
                }
                finally
                {
                    _isReplayNavigation.Value = false;
                }
            }
        );

    /// <summary>
    /// Rehydrates a restored intent's <c>[JsonIgnore]</c> state before navigating with it:
    /// walks the already-restored stack from the TOP page down to the root and awaits the
    /// FIRST lifecycle target implementing <see cref="IIntentHydrator{TIntent}"/> for the
    /// intent's type (contravariant match — a base-type hydrator qualifies). The
    /// initialization root can hydrate too: it is on the stack when the root chunk replays.
    /// </summary>
    private async ValueTask HydrateIntentAsync(object intent)
    {
        if (GetCurrentStack() is not { Count: > 0 } stack)
        {
            return;
        }

        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var target = NavigationHelper.GetLifecycleTarget(stack[i].Page);

            // Method-signature lookup, the engine's established pattern (see
            // NavigationHelper's OnEnteringAsync dispatch): matches implicit AND explicit
            // IIntentHydrator<TIntent> implementations, including contravariant ones
            // (a base-type hydrator qualifies via parameter assignability).
#pragma warning disable IL2075 // Page models are registered with NavigationConfigurator.DynamicallyAccessedPageModelMembers (methods preserved).
            var hydrateMethod = target.GetType()
                                      .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                                      .FirstOrDefault(candidate =>
                                          {
                                              if (!candidate.Name.EndsWith("HydrateAsync", StringComparison.Ordinal) || candidate.ReturnType != typeof(ValueTask))
                                              {
                                                  return false;
                                              }

                                              var parameters = candidate.GetParameters();

                                              return parameters.Length == 1 && intent.GetType().IsAssignableTo(parameters[0].ParameterType);
                                          }
                                      );
#pragma warning restore IL2075

            if (hydrateMethod is not null)
            {
                await ((ValueTask) hydrateMethod.Invoke(target, [intent])!);

                return;
            }
        }
    }

    private static IEnumerable<(IReadOnlyList<NavigationRestoreFrame> Frames, object? Intent)> ChunkFrames(IReadOnlyList<NavigationRestoreFrame> frames)
    {
        var current = new List<NavigationRestoreFrame>();

        foreach (var frame in frames)
        {
            current.Add(frame);

            if (frame.Intent is not null)
            {
                yield return (current, frame.Intent);

                current = [];
            }
        }

        if (current.Count > 0)
        {
            yield return (current, null);
        }
    }

    #endregion

    #region Intent recording & capture

    private PageRestoreState GetState(Page page) => _pageStates.GetOrCreateValue(page);

    /// <summary>
    /// Records the intent a page was entered with. Restorability derives from it: no intent ⇒
    /// restorable; a REGISTERED intent type ⇒ restorable with intent (serialized as-is —
    /// <c>[JsonIgnore]</c> properties are rehydrated at replay via
    /// <see cref="IIntentHydrator{TIntent}"/>); an unregistered type (or a serialization
    /// failure) ⇒ the restorable stack ends at this page (its context cannot be reproduced;
    /// registration is also what keeps the type's members trim-safe for the serializer).
    /// </summary>
    private void RecordIntent(Page page, object? intent)
    {
        if (intent is null)
        {
            return;
        }

        var state = GetState(page);

        if (_options!.IntentIdsByType.TryGetValue(intent.GetType(), out var typeId))
        {
            try
            {
                state.Intent = new NavigationRestoreIntentData { TypeId = typeId, Payload = IntentSerializer.Serialize(intent) };
                state.NonRestorable = false;

                return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to serialize restore intent {IntentType}; the page will not restore.", intent.GetType().FullName);
            }
        }

        state.NonRestorable = true;
    }

    private IReadOnlyList<NavigationStackPage>? GetCurrentStack()
        => NavigationService.ShellProxyOrDefault is { } proxy
            ? proxy.CurrentItem.CurrentSection.GetNavigationStack().ToList()
            : null;

    private void ScheduleCapture()
    {
        if (Capture())
        {
            DebounceWrite();
        }
    }

    /// <summary>
    /// Rebuilds the snapshot payload from the LIVE stack and the recorded page states: popped
    /// pages fall out by construction; a non-restorable page ends the restorable prefix. A
    /// non-restorable ROOT produces an intentionally invalid snapshot (RootSegment null) so
    /// the next boot restores nothing — but never resurrects an older state.
    /// </summary>
    private bool Capture()
    {
        try
        {
            var proxy = NavigationService.ShellProxyOrDefault;

            if (proxy is null)
            {
                return false;
            }

            var section = proxy.CurrentItem.CurrentSection;
            var stack = section.GetNavigationStack().ToList();

            if (stack.Count == 0)
            {
                return false;
            }

            var snapshot = new NavigationRestoreSnapshot
            {
                SchemaVersion = _schemaVersion,
                AppVersion = AppVersionProvider(),
                RouteHash = ComputeRouteHash(proxy),
                CapturedAt = _timeProvider.GetUtcNow()
            };

            var rootRestorable = !_pageStates.TryGetValue(stack[0].Page, out var rootState) || !rootState.NonRestorable;

            if (rootRestorable)
            {
                snapshot.RootSegment = stack[0].SegmentName;
                snapshot.RootIntent = rootState?.Intent;

                foreach (var entry in stack.Skip(1))
                {
                    _pageStates.TryGetValue(entry.Page, out var state);

                    if (state?.NonRestorable == true)
                    {
                        // The restorable prefix ends here: pages above build on this one.
                        break;
                    }

                    snapshot.Frames.Add(new NavigationRestoreFrameData { Segment = entry.SegmentName, Intent = state?.Intent });
                }
            }

            _pendingPayload = JsonSerializer.Serialize(snapshot, NavigationRestoreJsonContext.Default.NavigationRestoreSnapshot);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Navigation-state snapshot capture failed.");

            return false;
        }
    }

    private Task CaptureAndFlushAsync() => Capture() ? FlushAsync() : Task.CompletedTask;

    #endregion

    #region Persistence

    private void DebounceWrite()
    {
        _writeDebounceCts?.Cancel();
        _writeDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _writeDebounceCts = cts;

        _ = DebouncedWriteAsync(cts.Token);
    }

    private async Task DebouncedWriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_writeDebounce, _timeProvider, cancellationToken).ConfigureAwait(false);
            await WritePendingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Debounce superseded (or shutdown): the newer write carries the newer snapshot.
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Navigation-state snapshot write failed.");
        }
    }

    /// <summary>Captures and writes immediately, bypassing debounce (backgrounding, tests).</summary>
    internal async Task FlushAsync()
    {
        if (_writeDebounceCts is { } cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        await WritePendingAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SwallowedFlushAsync()
    {
        try
        {
            Capture();
            await FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Navigation-state snapshot write failed.");
        }
    }

    private Task WritePendingAsync(CancellationToken cancellationToken)
        => _pendingPayload is { } payload ? Store.WriteAsync(payload, cancellationToken) : Task.CompletedTask;

    private void HookWindows()
    {
        // Backgrounding is the last reliable moment before a potential process death: flush
        // immediately, skipping debounce. Best-effort — Application.Current is absent in
        // unit-test hosts, and windows created later are picked up on the next boot hook.
        try
        {
            if (Application.Current is not { } application)
            {
                return;
            }

            foreach (var window in application.Windows)
            {
                if (_hookedWindows.Add(window))
                {
                    window.Deactivated += OnWindowBackgrounding;
                    window.Stopped += OnWindowBackgrounding;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not hook window lifecycle events for restore flushing.");
        }
    }

    private void OnWindowBackgrounding(object? sender, EventArgs e) => _ = SwallowedFlushAsync();

    #endregion

    #region Validation helpers

    /// <summary>Returns the deserialized intent, or null when the type id is not registered (renamed/removed).</summary>
    private object? DeserializeIntent(NavigationRestoreIntentData data)
    {
        if (data.TypeId is null || data.Payload is null || !_options!.IntentTypesById.TryGetValue(data.TypeId, out var intentType))
        {
            return null;
        }

        return IntentSerializer.Deserialize(intentType, data.Payload);
    }

    internal string ComputeRouteHash(IShellProxy proxy)
        => NavigationRestoreRouteHash.Compute(
            GetOrderedRootSegments(proxy),
            GetRegisteredPageTypes().Select(NavigationSegmentAttribute.GetSegmentName),
            _options!.IntentTypesById.Keys
        );

    private static List<string> GetOrderedRootSegments(IShellProxy proxy)
        => proxy.Items
                .SelectMany(item => item.Sections)
                .SelectMany(section => section.Contents)
                .Select(content => content.SegmentName)
                .ToList();

    /// <summary>
    /// Every destination type registered with the navigation engine: model-mapped pages
    /// (<c>Mapping</c> values) AND view-only/component registrations (which never enter the
    /// mapping — it is keyed by page-model type).
    /// </summary>
    private IEnumerable<Type> GetRegisteredPageTypes()
    {
        var configuration = NavigationService.Configuration;

        return configuration is NavigationConfigurator configurator
            ? configuration.Mapping.Values.Concat(configurator.ViewOnlyPages).Concat(configurator.ComponentPages)
            : configuration.Mapping.Values;
    }

    /// <summary>
    /// Maps segment names to replayable page types: every registered page plus the host's
    /// root contents. Pages navigated to via unregistered page types are not resolvable —
    /// their frames truncate the restored prefix.
    /// </summary>
    private Dictionary<string, Type> BuildSegmentTypeMap(IShellProxy proxy)
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var pageType in GetRegisteredPageTypes())
        {
            map.TryAdd(NavigationSegmentAttribute.GetSegmentName(pageType), pageType);
        }

        foreach (var content in proxy.Items.SelectMany(item => item.Sections).SelectMany(section => section.Contents))
        {
            // Root contents resolve by segment; the page type is only needed for pushes, but
            // a root page type registered nowhere else can still be pushed after restore.
            if (content.Page is { } page)
            {
                map.TryAdd(content.SegmentName, page.GetType());
            }
        }

        return map;
    }

    private static string GetDefaultAppVersion()
    {
        try
        {
            var appInfo = AppInfo.Current;

            return $"{appInfo.VersionString}+{appInfo.BuildString}";
        }
        catch
        {
            // Non-platform hosts (unit tests) have no AppInfo implementation.
            return "unknown";
        }
    }

    #endregion

    public void Dispose()
    {
        foreach (var window in _hookedWindows)
        {
            window.Deactivated -= OnWindowBackgrounding;
            window.Stopped -= OnWindowBackgrounding;
        }

        _hookedWindows.Clear();
        _writeDebounceCts?.Cancel();
        _writeDebounceCts?.Dispose();
        _writeDebounceCts = null;
    }
}
