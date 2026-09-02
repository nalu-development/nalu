using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// The declaration of a Magnet layout: virtual nodes (barriers, guidelines, chains) and view constraints.
/// </summary>
/// <remarks>
/// A definition is a pure declaration and can be SHARED across layouts (e.g. as a resource used by several
/// <see cref="Magnet" />s, or by every cell of a template): the per-layout state — view bindings, compiled indexes,
/// inline nodes — lives in each attached <see cref="Magnet" />. Mutating a shared definition updates every layout
/// using it.
/// </remarks>
[ContentProperty(nameof(MagnetNodes))]
public sealed class MagnetDefinition : IMagnetOwner
{
    private readonly ObservableCollection<MagnetNode> _nodes = [];
    private readonly Dictionary<string, MagnetNode> _byId = new(StringComparer.Ordinal);
    private readonly List<MagnetNode> _declared = [];
    private readonly List<IMagnetOwner> _owners = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MagnetDefinition" /> class.
    /// </summary>
    public MagnetDefinition()
    {
        _nodes.CollectionChanged += OnNodesChanged;
    }

    /// <summary>
    /// Gets the nodes declared by this definition.
    /// </summary>
    public IList<MagnetNode> MagnetNodes => _nodes;

    /// <summary>
    /// Gets the number of declared nodes.
    /// </summary>
    internal int Count => _nodes.Count;

    /// <summary>
    /// Enumerates the declared nodes.
    /// </summary>
    internal IEnumerable<MagnetNode> AllNodes => _nodes;

    /// <summary>
    /// Copies the declared nodes into <paramref name="target" />.
    /// </summary>
    internal void CopyNodesTo(List<MagnetNode> target)
    {
        foreach (var node in _nodes)
        {
            target.Add(node);
        }
    }

    /// <summary>
    /// Adds a node (fluent).
    /// </summary>
    public MagnetDefinition Add(MagnetNode node)
    {
        _nodes.Add(node);

        return this;
    }

    /// <summary>
    /// Adds nodes (fluent).
    /// </summary>
    public MagnetDefinition Add(params MagnetNode[] nodes)
    {
        foreach (var node in nodes)
        {
            _nodes.Add(node);
        }

        return this;
    }

    /// <summary>
    /// Subscribes a layout to this definition's change notifications.
    /// </summary>
    internal void Attach(IMagnetOwner owner)
    {
        if (!_owners.Contains(owner))
        {
            _owners.Add(owner);
        }
    }

    /// <summary>
    /// Unsubscribes a layout.
    /// </summary>
    internal void Detach(IMagnetOwner owner) => _owners.Remove(owner);

    void IMagnetOwner.OnNodeChanged(MagnetNode? node, MagnetChange change)
    {
        for (var i = 0; i < _owners.Count; i++)
        {
            _owners[i].OnNodeChanged(node, change);
        }
    }

    void IMagnetOwner.OnApplyVisibilityRequested(MagnetView node)
    {
        for (var i = 0; i < _owners.Count; i++)
        {
            _owners[i].OnApplyVisibilityRequested(node);
        }
    }

    private void Register(MagnetNode node)
    {
        var id = node.MagnetId;

        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidOperationException($"Every MagnetNode requires a MagnetId ({node.GetType().Name} declared in the MagnetDefinition).");
        }

        if (string.Equals(id, MagnetAnchor.Parent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"MagnetId '{id}' is reserved for the stage.");
        }

        if (_byId.TryGetValue(id, out var existing))
        {
            if (ReferenceEquals(existing, node))
            {
                return;
            }

            throw new InvalidOperationException($"MagnetId '{id}' is declared more than once in the MagnetDefinition.");
        }

        if (node.Definition is not null && !ReferenceEquals(node.Definition, this))
        {
            throw new InvalidOperationException($"Magnet node '{id}' already belongs to another MagnetDefinition.");
        }

        node.Origin = MagnetNodeOrigin.Definition;
        node.Definition = this;
        node.Attach(this);
        _byId[id] = node;
    }

    internal bool TryGet(string id, [NotNullWhen(true)] out MagnetNode? node) => _byId.TryGetValue(id, out node);

    internal void OnNodeIdChanged(MagnetNode node, string? oldId, string? newId)
    {
        if (oldId is not null && _byId.TryGetValue(oldId, out var existing) && ReferenceEquals(existing, node))
        {
            _byId.Remove(oldId);
        }

        if (string.IsNullOrEmpty(newId))
        {
            throw new InvalidOperationException("Every MagnetNode requires a MagnetId (it cannot be cleared once registered).");
        }

        if (_byId.TryGetValue(newId, out var other) && !ReferenceEquals(other, node))
        {
            throw new InvalidOperationException($"MagnetId '{newId}' is declared more than once in the MagnetDefinition.");
        }

        _byId[newId] = node;
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var node in _declared)
            {
                Forget(node);
            }

            _declared.Clear();
        }

        if (e.OldItems is not null)
        {
            foreach (MagnetNode node in e.OldItems)
            {
                Forget(node);
                _declared.Remove(node);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (MagnetNode node in e.NewItems)
            {
                Register(node);
                _declared.Add(node);
            }
        }

        ((IMagnetOwner) this).OnNodeChanged(null, MagnetChange.Structure);
    }

    private void Forget(MagnetNode node)
    {
        if (node.MagnetId is { } id && _byId.TryGetValue(id, out var existing) && ReferenceEquals(existing, node))
        {
            _byId.Remove(id);
        }

        node.Definition = null;
        node.Detach();
    }
}
