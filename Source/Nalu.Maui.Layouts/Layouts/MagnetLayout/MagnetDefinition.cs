using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// The per-layout registry of <see cref="MagnetNode" />s: virtual nodes (barriers, guidelines, chains) and view constraints.
/// </summary>
/// <remarks>
/// A definition is stateful and belongs to exactly one <see cref="Magnet" />: never share an instance across layouts
/// (declaring one inline inside a <c>DataTemplate</c> is fine, each inflation creates a fresh instance).
/// </remarks>
[ContentProperty(nameof(MagnetNodes))]
public sealed class MagnetDefinition : BindableObject
{
    private readonly ObservableCollection<MagnetNode> _nodes = [];
    private readonly Dictionary<string, MagnetNode> _byId = new(StringComparer.Ordinal);
    private readonly List<MagnetNode> _viewNodes = [];
    private readonly List<MagnetNode> _declared = [];

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

    internal IMagnetOwner? Owner { get; private set; }

    /// <summary>
    /// Gets the number of registered nodes (declared + inline).
    /// </summary>
    internal int Count => _byId.Count;

    /// <summary>
    /// Enumerates all registered nodes: declared nodes first, then inline (view) nodes.
    /// </summary>
    internal IEnumerable<MagnetNode> AllNodes
    {
        get
        {
            foreach (var node in _nodes)
            {
                yield return node;
            }

            foreach (var node in _viewNodes)
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Snapshots all registered nodes into an array (declared first, then inline).
    /// </summary>
    internal MagnetNode[] AllNodesArray()
    {
        var result = new MagnetNode[_nodes.Count + _viewNodes.Count];
        _nodes.CopyTo(result, 0);
        _viewNodes.CopyTo(result, _nodes.Count);

        return result;
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

    internal void Attach(IMagnetOwner owner)
    {
        if (Owner is not null && !ReferenceEquals(Owner, owner))
        {
            throw new InvalidOperationException(
                "This MagnetDefinition already belongs to another Magnet layout: a definition cannot be shared across layouts. " +
                "Do not declare it as an application-wide StaticResource; declare it inline (or inside the DataTemplate)."
            );
        }

        Owner = owner;

        foreach (var node in AllNodes)
        {
            node.Attach(owner);
        }
    }

    internal void Detach()
    {
        foreach (var node in AllNodes)
        {
            node.Detach();
        }

        Owner = null;
    }

    internal void Register(MagnetNode node, MagnetNodeOrigin origin)
    {
        var id = node.MagnetId;

        if (string.IsNullOrEmpty(id))
        {
            throw new InvalidOperationException($"Every MagnetNode requires a MagnetId ({node.GetType().Name} declared {Describe(origin)}).");
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

            throw new InvalidOperationException(
                $"MagnetId '{id}' is defined both {Describe(existing.Origin)} and {Describe(origin)}."
            );
        }

        if (node.Definition is not null && !ReferenceEquals(node.Definition, this))
        {
            throw new InvalidOperationException($"Magnet node '{id}' already belongs to another MagnetDefinition.");
        }

        node.Origin = origin;
        node.Definition = this;
        _byId[id] = node;

        if (origin == MagnetNodeOrigin.View)
        {
            _viewNodes.Add(node);
        }

        if (Owner is { } owner)
        {
            node.Attach(owner);
            owner.OnNodeChanged(node, MagnetChange.Structure);
        }
    }

    /// <summary>
    /// Removes a view-origin node; definition-origin nodes are kept.
    /// </summary>
    internal void Unregister(MagnetNode node)
    {
        if (node.Origin != MagnetNodeOrigin.View || !ReferenceEquals(node.Definition, this))
        {
            return;
        }

        if (node.MagnetId is { } id && _byId.TryGetValue(id, out var existing) && ReferenceEquals(existing, node))
        {
            _byId.Remove(id);
        }

        _viewNodes.Remove(node);
        node.Definition = null;
        node.Detach();
        Owner?.OnNodeChanged(node, MagnetChange.Structure);
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
            throw new InvalidOperationException($"MagnetId '{newId}' is defined both {Describe(other.Origin)} and {Describe(node.Origin)}.");
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
                Register(node, MagnetNodeOrigin.Definition);
                _declared.Add(node);
            }
        }

        Owner?.OnNodeChanged(null, MagnetChange.Structure);
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

    private static string Describe(MagnetNodeOrigin origin)
        => origin == MagnetNodeOrigin.Definition ? "in the MagnetDefinition" : "inline on a child view";
}
