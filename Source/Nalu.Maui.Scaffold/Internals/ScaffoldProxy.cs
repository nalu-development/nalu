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
    private readonly List<ScaffoldAreaProxy> _areas;
    private readonly Dictionary<string, ScaffoldRootProxy> _rootsBySegment;

    private ScaffoldAreaProxy _currentArea;
    private string _location = string.Empty;

    private bool _batching;
    private ScaffoldRootProxy? _batchRoot;
    private List<NavigationStackPage> _pendingPushes = [];
    private int _batchPopCount;
    private bool _batchRootChanged;

    public IShellItemProxy CurrentItem => _currentArea;

    public IReadOnlyList<IShellItemProxy> Items => _areas;

    public string Location => _location;

    public string State => "//" + string.Join('/', _currentArea.CurrentRoot.GetNavigationStack().Select(p => p.SegmentName));

    public ScaffoldProxy(Scaffold scaffold, NavigationService navigationService)
    {
        _scaffold = scaffold;

        if (scaffold.Areas.Count == 0)
        {
            throw new InvalidOperationException("A Scaffold must contain at least one ScaffoldArea.");
        }

        // P0: the structure is captured once; dynamic Areas/Roots mutation is not supported yet.
        _areas = scaffold.Areas.Select((area, index) => new ScaffoldAreaProxy(area, index, this, navigationService)).ToList();
        _rootsBySegment = new Dictionary<string, ScaffoldRootProxy>(StringComparer.Ordinal);

        foreach (var rootProxy in _areas.SelectMany(a => a.Roots))
        {
            // First registration wins when the same page type roots multiple stacks.
            _rootsBySegment.TryAdd(rootProxy.SegmentName, rootProxy);
        }

        _currentArea = _areas[0];
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

        if (rootChanged)
        {
            ApplySelection(targetRoot);
        }

        _location = model.PushedPages.Count > 0 ? model.PushedPages[^1].Route : targetRoot.BaseRoute;

        var hint = rootChanged ? ScaffoldPresentationHint.None
            : pendingPushes.Count > 0 ? ScaffoldPresentationHint.Push
            : popCount > 0 ? ScaffoldPresentationHint.Pop
            : ScaffoldPresentationHint.None;

        if (_scaffold.Presenter is { } presenter)
        {
            await presenter.SynchronizeAsync(targetRoot.Root, hint).ConfigureAwait(true);
        }

        completeAction?.Invoke();
    }

    public IShellContentProxy GetContent(string segmentName) => GetRoot(segmentName);

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
    }
}
