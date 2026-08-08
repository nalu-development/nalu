using System.Collections.Specialized;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The Scaffold's implementation of the engine-facing navigation host contract.
/// Written from scratch (no code shared with the Shell adapter): no URI marshalling, no global
/// route table, no animation-settling delays — navigation mutates the stack model directly and
/// each commit drives a single presenter synchronization.
/// </summary>
/// <remarks>
/// Batch semantics (mirroring the engine's expectations, verified against the Shell adapter):
/// pops apply to the model immediately (the engine has already run the leaving lifecycle and
/// disposes those pages — deferring them would leave ghost entries in stacks the engine considers
/// clean), while pushes stay pending until <see cref="CommitNavigationAsync"/> so a multi-push
/// batch is presented as one transition. Guard checks commit mid-navigation and re-begin,
/// which simply produces an extra synchronization showing the intermediate state.
/// </remarks>
internal sealed class ScaffoldProxy : IShellProxy, IDisposable
{
    private readonly Scaffold _scaffold;
    private readonly NavigationService _navigationService;
    private readonly List<ScaffoldAreaProxy> _areas;
    private readonly Dictionary<string, ScaffoldRootProxy> _rootsBySegment;

    private ScaffoldAreaProxy _currentArea;
    private string _location = string.Empty;
    private int _nextAreaIndex;
    private bool _structureSyncScheduled;

    private bool _batching;
    private ScaffoldRootProxy? _batchRoot;
    private List<NavigationStackPage> _pendingPushes = [];
    private int _batchPopCount;
    private bool _batchRootChanged;

    public IShellItemProxy CurrentItem => _currentArea;

    public IReadOnlyList<IShellItemProxy> Items => _areas;

    public string Location => _location;

    public string State => "//" + string.Join('/', _currentArea.CurrentRoot.GetNavigationStack().Select(p => p.SegmentName));

    /// <summary>The scaffold this proxy drives (host-specific services resolve it through here).</summary>
    internal Scaffold Scaffold => _scaffold;

    public ScaffoldProxy(Scaffold scaffold, NavigationService navigationService)
    {
        _scaffold = scaffold;
        _navigationService = navigationService;

        if (scaffold.Areas.Count == 0)
        {
            throw new InvalidOperationException("A Scaffold must contain at least one ScaffoldArea.");
        }

        _areas = scaffold.Areas.Select((area, index) => new ScaffoldAreaProxy(area, index, this, navigationService)).ToList();
        _rootsBySegment = new Dictionary<string, ScaffoldRootProxy>(StringComparer.Ordinal);

        foreach (var rootProxy in _areas.SelectMany(a => a.Roots))
        {
            // First registration wins when the same page type roots multiple stacks.
            _rootsBySegment.TryAdd(rootProxy.SegmentName, rootProxy);
        }

        _currentArea = _areas[0];

        // LIVE structure: Areas/Roots mutations after initialization (runtime edits, XAML hot
        // reload re-inflation) re-sync the proxy tree — surviving instances keep their proxies
        // (and stacks), removed roots dispose their content, and a vanished current root is
        // re-presented through a real engine navigation.
        _nextAreaIndex = _areas.Count;

        if (scaffold.Areas is INotifyCollectionChanged observableAreas)
        {
            observableAreas.CollectionChanged += OnStructureCollectionChanged;
        }

        foreach (var areaProxy in _areas)
        {
            ObserveAreaRoots(areaProxy.Area);
        }
    }

    private void ObserveAreaRoots(ScaffoldArea area)
    {
        if (area.Roots is INotifyCollectionChanged observableRoots)
        {
            observableRoots.CollectionChanged += OnStructureCollectionChanged;
        }
    }

    private void UnobserveAreaRoots(ScaffoldArea area)
    {
        if (area.Roots is INotifyCollectionChanged observableRoots)
        {
            observableRoots.CollectionChanged -= OnStructureCollectionChanged;
        }
    }

    private void OnStructureCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Structure edits arrive in bursts (XAML hot reload re-runs the whole initialization):
        // coalesce into a single sync on the dispatcher. Also required for reentrancy —
        // ObservableCollection forbids mutating from inside its own change event.
        if (_structureSyncScheduled)
        {
            return;
        }

        _structureSyncScheduled = true;

        _scaffold.Dispatcher.Dispatch(() =>
        {
            _structureSyncScheduled = false;
            SyncStructure();
        });
    }

    private void SyncStructure()
    {
        if (_scaffold.Areas.Count == 0 || _scaffold.Areas.All(a => a.Roots.Count == 0))
        {
            // Transient empty structure (mid re-inflation): keep the last good tree.
            return;
        }

        var previousAreas = _areas.ToList();
        var previousCurrentRoot = _currentArea.CurrentRoot;
        var removedRoots = new List<ScaffoldRootProxy>();

        var byInstance = previousAreas.ToDictionary(a => a.Area, a => a);
        var rebuilt = new List<ScaffoldAreaProxy>();

        foreach (var area in _scaffold.Areas)
        {
            if (area.Roots.Count == 0)
            {
                // Skeleton area mid-inflation: ignore until populated (a later change re-syncs).
                continue;
            }

            if (byInstance.TryGetValue(area, out var existing))
            {
                removedRoots.AddRange(existing.SyncRoots(_navigationService));
                rebuilt.Add(existing);
            }
            else
            {
                ObserveAreaRoots(area);
                rebuilt.Add(new ScaffoldAreaProxy(area, _nextAreaIndex++, this, _navigationService));
            }
        }

        if (rebuilt.Count == 0)
        {
            return;
        }

        foreach (var removedArea in previousAreas.Where(a => !rebuilt.Contains(a)))
        {
            UnobserveAreaRoots(removedArea.Area);
            removedRoots.AddRange(removedArea.Roots);
        }

        _areas.Clear();
        _areas.AddRange(rebuilt);

        _rootsBySegment.Clear();

        foreach (var rootProxy in _areas.SelectMany(a => a.Roots))
        {
            _rootsBySegment.TryAdd(rootProxy.SegmentName, rootProxy);
        }

        // GLOBAL adoption pass (whole-area replacement, e.g. hot reload re-adding the entire
        // structure): a removed root whose segment reappears in the new tree hands its live
        // stack to the fresh proxy instead of being disposed.
        for (var i = removedRoots.Count - 1; i >= 0; i--)
        {
            var removed = removedRoots[i];

            if (_rootsBySegment.TryGetValue(removed.SegmentName, out var successor)
                && !ReferenceEquals(successor, removed)
                && successor.Page is null)
            {
                successor.AdoptStackFrom(removed);
                removedRoots.RemoveAt(i);
            }
        }

        var treeRoots = _areas.SelectMany(a => a.Roots).ToList();

        if (treeRoots.Contains(previousCurrentRoot))
        {
            // The presented root's proxy survived (possibly reused across the rebuild): keep it
            // selected.
            _currentArea = previousCurrentRoot.Area;
            _currentArea.CurrentRoot = previousCurrentRoot;

            foreach (var removed in removedRoots)
            {
                DisposeRemovedRoot(removed);
            }
        }
        else if (_rootsBySegment.TryGetValue(previousCurrentRoot.SegmentName, out var replacement)
                 && replacement.Page is not null)
        {
            // Same-segment replacement that adopted the live stack: presentation continuity —
            // the very same page instances are now owned by the new proxies. A light presenter
            // sync rebinds the platform side to the new ScaffoldRoot instance.
            _currentArea = replacement.Area;
            _currentArea.CurrentRoot = replacement;
            _location = replacement.Root.NavigationStack.PushedPages.Count > 0
                ? replacement.Root.NavigationStack.PushedPages[^1].Route
                : replacement.BaseRoute;

            foreach (var removed in removedRoots)
            {
                DisposeRemovedRoot(removed);
            }

            _scaffold.Presenter?.SynchronizeAsync(replacement.Root, ScaffoldPresentationHint.None).FireAndForget();
        }
        else
        {
            // The presented root is GONE from the new structure: keep its proxy alive as the
            // engine's current section (the engine pops its pages with real lifecycle) and
            // navigate to the new tree's first root. Its disposal happens via the engine's
            // dispose bag; leftovers are cleaned after the navigation completes.
            var keepAlive = previousCurrentRoot;
            removedRoots.Remove(keepAlive);

            // Pin the current section back to the removed root's LIVE stack until the
            // navigation commits — SyncRoots already re-pointed the area's CurrentRoot to a
            // fallback whose stack is empty, and the engine cannot navigate from an empty
            // section. The commit's ApplySelection moves selection into the new tree.
            _currentArea = keepAlive.Area;
            _currentArea.CurrentRoot = keepAlive;

            // The engine resolves contents BY SEGMENT while navigating away (e.g. re-selecting
            // the leaving content): the removed root must stay resolvable until the fallback
            // completes. Its segment is absent from the new tree, so there is no collision.
            _rootsBySegment[keepAlive.SegmentName] = keepAlive;

            foreach (var removed in removedRoots)
            {
                DisposeRemovedRoot(removed);
            }

            // Target the first root of the NEW tree — preferring the area that hosted the
            // removed root. NOT CurrentRoot: the pin above points it back at the REMOVED root.
            var target = _areas.Contains(keepAlive.Area) && keepAlive.Area.Roots.Count > 0
                ? keepAlive.Area.Roots[0]
                : _areas[0].Roots[0];

            var navigation = new AbsoluteNavigation();
            IList<INavigationSegment> segments = navigation;

            segments.Add(
                new NavigationSegment
                {
                    SegmentName = target.SegmentName,
                    Type = target.PageType
                }
            );

            NavigateToFallbackAsync(navigation, keepAlive).FireAndForget();
        }
    }

    private async Task NavigateToFallbackAsync(AbsoluteNavigation navigation, ScaffoldRootProxy removedCurrentRoot)
    {
        try
        {
            await _navigationService.GoToAsync(navigation);
        }
        finally
        {
            if (_rootsBySegment.TryGetValue(removedCurrentRoot.SegmentName, out var mapped) && ReferenceEquals(mapped, removedCurrentRoot))
            {
                _rootsBySegment.Remove(removedCurrentRoot.SegmentName);
            }

            // The engine popped and disposed the removed root's pages through its own
            // lifecycle; anything left (e.g. a never-created root page) is released here.
            DisposeRemovedRoot(removedCurrentRoot);
        }
    }

    private static void DisposeRemovedRoot(ScaffoldRootProxy rootProxy)
    {
        foreach (var entry in rootProxy.Root.NavigationStack.RemoveFromTop())
        {
            DisconnectHandlerHelper.DisconnectHandlers(entry.Page);
            PageNavigationContext.Dispose(entry.Page);
        }

        if (rootProxy.Page is { } rootPage)
        {
            DisconnectHandlerHelper.DisconnectHandlers(rootPage);
            rootProxy.DestroyContent();
        }
    }

    /// <summary>Resolves the initial content segment from an optional page (or page-model) type.</summary>
    public string ResolveInitialSegmentName(Type? initialRootPageType, INavigationConfiguration configuration)
    {
        if (initialRootPageType is null)
        {
            return _areas[0].CurrentRoot.SegmentName;
        }

        var pageType = NavigationHelper.GetPageType(initialRootPageType, configuration);
        var segmentName = NavigationSegmentAttribute.GetSegmentName(pageType);

        return _rootsBySegment.ContainsKey(segmentName)
            ? segmentName
            : throw new InvalidOperationException($"InitialRootPageType '{initialRootPageType.Name}' does not match any ScaffoldRoot.");
    }

    public bool BeginNavigation()
    {
        if (_batching)
        {
            return false;
        }

        _batching = true;
        _batchRoot = _currentArea.CurrentRoot;
        _pendingPushes = [];
        _batchPopCount = 0;
        _batchRootChanged = false;

        return true;
    }

    public bool ProposeNavigation(INavigationInfo navigation) => true;

    public Task PushAsync(string segmentName, Page page)
    {
        var targetRoot = EnsureBatch();
        var model = targetRoot.Root.NavigationStack;

        var baseRoute = _pendingPushes.Count > 0 ? _pendingPushes[^1].Route
            : model.PushedPages.Count > 0 ? model.PushedPages[^1].Route
            : targetRoot.BaseRoute;

        var isModal = Shell.GetPresentationMode(page).HasFlag(PresentationMode.Modal);
        _pendingPushes.Add(new NavigationStackPage($"{baseRoute}/{segmentName}", segmentName, page, isModal));

        return Task.CompletedTask;
    }

    public Task PopAsync(IShellSectionProxy section)
    {
        var targetRoot = EnsureBatch();

        if (!ReferenceEquals(section, targetRoot))
        {
            // Trimming a stack that is not the navigation target: applied directly,
            // page disposal is handled by the engine.
            section.RemoveStackPages(1);

            return Task.CompletedTask;
        }

        if (_pendingPushes.Count > 0)
        {
            _pendingPushes.RemoveAt(_pendingPushes.Count - 1);
        }
        else
        {
            targetRoot.Root.NavigationStack.RemoveFromTop(1);
            _batchPopCount++;
        }

        return Task.CompletedTask;
    }

    public Task SelectContentAsync(string segmentName)
    {
        var currentTarget = EnsureBatch();
        var target = GetRoot(segmentName);

        if (ReferenceEquals(target, currentTarget))
        {
            return Task.CompletedTask;
        }

        // Pending pushes on the abandoned target are discarded (the engine only switches
        // content after fully unwinding it; mirrors the Shell adapter's route replacement).
        _batchRootChanged = true;
        _batchRoot = target;
        _pendingPushes = [];
        _batchPopCount = 0;

        return Task.CompletedTask;
    }

    public async Task CommitNavigationAsync(Action? completeAction = null)
    {
        if (!_batching)
        {
            completeAction?.Invoke();

            return;
        }

        var targetRoot = _batchRoot!;
        var pendingPushes = _pendingPushes;
        var popCount = _batchPopCount;
        var rootChanged = _batchRootChanged;

        _batching = false;
        _batchRoot = null;
        _pendingPushes = [];
        _batchPopCount = 0;
        _batchRootChanged = false;

        if (!rootChanged && pendingPushes.Count == 0 && popCount == 0)
        {
            completeAction?.Invoke();

            return;
        }

        var model = targetRoot.Root.NavigationStack;

        foreach (var entry in pendingPushes)
        {
            model.Push(entry);
        }

        var previousRoot = _currentArea.CurrentRoot;

        if (rootChanged)
        {
            ApplySelection(targetRoot);
        }

        _location = model.PushedPages.Count > 0 ? model.PushedPages[^1].Route : targetRoot.BaseRoute;

        var hint = rootChanged ? ComputeRootSwitchHint(previousRoot, targetRoot)
            : pendingPushes.Count > 0 ? ScaffoldPresentationHint.Push
            : popCount > 0 ? ScaffoldPresentationHint.Pop
            : ScaffoldPresentationHint.None;

        if (_scaffold.Presenter is { } presenter)
        {
            await presenter.SynchronizeAsync(targetRoot.Root, hint);
        }

        completeAction?.Invoke();
    }

    public IShellContentProxy GetContent(string segmentName) => GetRoot(segmentName);

    /// <summary>
    /// Builds the absolute navigation realizing a tab/flyout selection of the given root:
    /// the root segment plus the root's preserved pushed-page segments (so the engine's
    /// stack matching keeps them — a bare root navigation would pop them), or the root segment
    /// alone when the root is already current (pop-to-root through guards).
    /// Returns null when the root is unknown or already current with an empty stack.
    /// </summary>
    public INavigationInfo? BuildRootSelectionNavigation(ScaffoldRoot root)
    {
        var rootProxy = _areas.SelectMany(a => a.Roots).FirstOrDefault(r => ReferenceEquals(r.Root, root));

        if (rootProxy is null)
        {
            return null;
        }

        var isCurrent = ReferenceEquals(_currentArea.CurrentRoot, rootProxy);
        var pushedPages = root.NavigationStack.PushedPages;

        if (isCurrent && pushedPages.Count == 0)
        {
            return null;
        }

        var navigation = new AbsoluteNavigation();
        IList<INavigationSegment> segments = navigation;

        segments.Add(
            new NavigationSegment
            {
                SegmentName = rootProxy.SegmentName,
                Type = rootProxy.PageType
            }
        );

        if (!isCurrent)
        {
            // Re-state the preserved stack so ToRelativeNavigation matches it segment by
            // segment: the switch restores the stack instead of unwinding it.
            foreach (var entry in pushedPages)
            {
                segments.Add(
                    new NavigationSegment
                    {
                        SegmentName = entry.SegmentName,
                        Type = entry.Page.GetType()
                    }
                );
            }
        }

        return navigation;
    }

    public void InitializeWithContent(string segmentName)
    {
        var target = GetRoot(segmentName);
        ApplySelection(target);
        _location = target.BaseRoute;
    }

    public void SendNavigationLifecycleEvent(NavigationLifecycleEventArgs args)
        => _scaffold.SendNavigationLifecycleEvent(args);

    public void Dispose()
    {
        if (_scaffold.Areas is INotifyCollectionChanged observableAreas)
        {
            observableAreas.CollectionChanged -= OnStructureCollectionChanged;
        }

        foreach (var areaProxy in _areas)
        {
            UnobserveAreaRoots(areaProxy.Area);
        }

        // Tear down all live pages: DI scopes and page models must be released when an app
        // discards the whole scaffold (e.g. a logout/login flow swapping the window page).
        foreach (var rootProxy in _areas.SelectMany(a => a.Roots))
        {
            if (rootProxy.Page is not { } rootPage)
            {
                continue;
            }

            foreach (var entry in rootProxy.Root.NavigationStack.RemoveFromTop())
            {
                DisconnectHandlerHelper.DisconnectHandlers(entry.Page);
                PageNavigationContext.Dispose(entry.Page);
            }

            DisconnectHandlerHelper.DisconnectHandlers(rootPage);
            rootProxy.DestroyContent();
        }
    }

    private ScaffoldRootProxy EnsureBatch()
        => _batching
            ? _batchRoot!
            : throw new NotSupportedException("This operation is not supported outside of a navigation batch.");

    private ScaffoldRootProxy GetRoot(string segmentName)
        => _rootsBySegment.TryGetValue(segmentName, out var rootProxy)
            ? rootProxy
            : throw new KeyNotFoundException($"No ScaffoldRoot found for segment '{segmentName}'.");

    /// <summary>
    /// Motion of a root switch: WITHIN an area the roots are neighbours on the same strip, so
    /// the switch travels — toward a higher structure ordinal ⇒ the new content slides in from
    /// the end edge, toward a lower one ⇒ from the start edge. ACROSS areas there is no shared
    /// strip to travel along: the switch cross-fades instead.
    /// </summary>
    private ScaffoldPresentationHint ComputeRootSwitchHint(ScaffoldRootProxy previousRoot, ScaffoldRootProxy targetRoot)
    {
        if (ReferenceEquals(previousRoot, targetRoot))
        {
            return ScaffoldPresentationHint.None;
        }

        if (!ReferenceEquals(previousRoot.Area, targetRoot.Area))
        {
            return ScaffoldPresentationHint.Fade;
        }

        var previousOrdinal = GetStructureOrdinal(previousRoot);
        var targetOrdinal = GetStructureOrdinal(targetRoot);

        return targetOrdinal.CompareTo(previousOrdinal) switch
        {
            > 0 => ScaffoldPresentationHint.SlideEnd,
            < 0 => ScaffoldPresentationHint.SlideStart,
            _ => ScaffoldPresentationHint.None
        };
    }

    private int GetStructureOrdinal(ScaffoldRootProxy rootProxy)
    {
        var ordinal = 0;

        foreach (var candidate in _areas.SelectMany(area => area.Roots))
        {
            if (ReferenceEquals(candidate, rootProxy))
            {
                return ordinal;
            }

            ordinal++;
        }

        return -1;
    }

    private void ApplySelection(ScaffoldRootProxy targetRoot)
    {
        var targetArea = targetRoot.Area;
        var previousArea = _currentArea;
        var previousRoot = previousArea.CurrentRoot;

        if (!ReferenceEquals(previousRoot, targetRoot))
        {
            previousRoot.Root.IsSelected = false;
        }

        if (!ReferenceEquals(previousArea, targetArea))
        {
            previousArea.Area.IsSelected = false;
        }

        _currentArea = targetArea;
        targetArea.CurrentRoot = targetRoot;
        targetArea.Area.CurrentRoot = targetRoot.Root;
        targetArea.Area.IsSelected = true;
        targetRoot.Root.IsSelected = true;
        _scaffold.CurrentArea = targetArea.Area;
        _scaffold.UpdateCurrentPage();
    }
}
