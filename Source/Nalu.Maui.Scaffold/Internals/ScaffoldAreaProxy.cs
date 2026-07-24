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
}
