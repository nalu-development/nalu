namespace Nalu;

/// <summary>Adapts a <see cref="ScaffoldArea"/> to the engine's item-level contract.</summary>
internal sealed class ScaffoldAreaProxy : IShellItemProxy
{
    private readonly List<ScaffoldRootProxy> _roots;

    public ScaffoldArea Area { get; }
    public string SegmentName { get; }
    public IShellProxy Parent { get; }

    /// <summary>The selected root proxy; updated by <see cref="ScaffoldProxy"/> on commit.</summary>
    public ScaffoldRootProxy CurrentRoot { get; set; }

    public IReadOnlyList<ScaffoldRootProxy> Roots => _roots;

    public IShellSectionProxy CurrentSection => CurrentRoot;

    public IReadOnlyList<IShellSectionProxy> Sections => _roots;

    public ScaffoldAreaProxy(ScaffoldArea area, int index, ScaffoldProxy parent, NavigationService navigationService)
    {
        Area = area;
        Parent = parent;
        // Area segments never surface to developers (type-based navigation); they only
        // give internal routes a stable, unique prefix.
        SegmentName = $"area{index}";
        _roots = area.Roots.Select(root => new ScaffoldRootProxy(root, this, navigationService)).ToList();

        if (_roots.Count == 0)
        {
            throw new InvalidOperationException("A ScaffoldArea must contain at least one ScaffoldRoot.");
        }

        CurrentRoot = _roots[0];
    }

    /// <summary>
    /// Re-syncs the root proxies against the area's live <see cref="ScaffoldArea.Roots"/>
    /// collection (runtime edits, XAML hot reload): surviving <see cref="ScaffoldRoot"/>
    /// instances keep their proxies (and stacks), new ones get fresh proxies, and a vanished
    /// <see cref="CurrentRoot"/> falls back by segment name, then to the first root.
    /// Returns the removed proxies for the caller to dispose.
    /// </summary>
    internal List<ScaffoldRootProxy> SyncRoots(NavigationService navigationService)
    {
        if (Area.Roots.Count == 0)
        {
            // Transient empty collection (mid re-inflation): keep the current roots.
            return [];
        }

        var previous = _roots.ToList();
        var byInstance = previous.ToDictionary(r => r.Root, r => r);
        var adopted = new HashSet<ScaffoldRootProxy>();
        _roots.Clear();

        foreach (var root in Area.Roots)
        {
            if (byInstance.TryGetValue(root, out var existing))
            {
                _roots.Add(existing);

                continue;
            }

            var created = new ScaffoldRootProxy(root, this, navigationService);

            // A fresh instance for an already-known segment (hot reload re-adds the structure
            // as new objects): the predecessor's live stack migrates into the new proxy.
            if (previous.FirstOrDefault(p => !adopted.Contains(p) && !Area.Roots.Contains(p.Root) && p.SegmentName == created.SegmentName) is { } predecessor)
            {
                created.AdoptStackFrom(predecessor);
                adopted.Add(predecessor);
            }

            _roots.Add(created);
        }

        var removed = previous.Where(r => !_roots.Contains(r) && !adopted.Contains(r)).ToList();

        if (!_roots.Contains(CurrentRoot))
        {
            CurrentRoot = _roots.FirstOrDefault(r => r.SegmentName == CurrentRoot.SegmentName) ?? _roots[0];
        }

        return removed;
    }
}
