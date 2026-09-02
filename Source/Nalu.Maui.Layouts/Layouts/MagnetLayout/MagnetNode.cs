using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// Receives change notifications from <see cref="MagnetNode" />s and <see cref="MagnetDefinition" />s.
/// </summary>
internal interface IMagnetOwner
{
    void OnNodeChanged(MagnetNode? node, MagnetChange change);
}

/// <summary>
/// Where a node was declared.
/// </summary>
internal enum MagnetNodeOrigin : byte
{
    /// <summary>Declared inside a <see cref="MagnetDefinition" />: survives view detach.</summary>
    Definition,

    /// <summary>Created by the per-view factory (<see cref="Magnet.GetConstraints" />): unregistered when the view is removed.</summary>
    View
}

/// <summary>
/// Base class of every element of a <see cref="MagnetDefinition" />.
/// </summary>
/// <remarks>
/// Nodes are plain <see cref="INotifyPropertyChanged" /> objects, not <c>BindableObject</c>s: they live outside the
/// visual tree, so no <c>BindingContext</c> reaches them — they can act as a binding SOURCE but not as a binding
/// target. Mutate them directly (optionally inside <c>Magnet.TransitionToAsync</c> to animate the change).
/// </remarks>
public abstract class MagnetNode : INotifyPropertyChanged
{
    private string? _magnetId;
    private uint _setMask;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Marks a property as explicitly assigned (assembly-internal replacement of <c>BindableObject.IsSet</c>,
    /// used by definition derivation to know which properties a node overrides). Marked on every assignment,
    /// including assignments of the current value.
    /// </summary>
    private protected void MarkSet(uint bit) => _setMask |= bit;

    /// <summary>Gets whether a property was explicitly assigned (bit constants are defined per node class).</summary>
    internal bool IsSet(uint bit) => (_setMask & bit) != 0;

    /// <summary>
    /// Gets or sets the identifier of this node. Required and unique within a <see cref="MagnetDefinition" />.
    /// </summary>
    /// <remarks>
    /// This is the only identity of a node: nothing falls back to <c>AutomationId</c>. It should not be changed after registration.
    /// </remarks>
    public string? MagnetId
    {
        get => _magnetId;
        set
        {
            if (string.Equals(_magnetId, value, StringComparison.Ordinal))
            {
                return;
            }

            var old = _magnetId;
            _magnetId = value;
            OnPropertyChanged();
            Definition?.OnNodeIdChanged(this, old, value);
            Notify(MagnetChange.Structure);
        }
    }

    internal MagnetNodeOrigin Origin { get; set; }

    internal IMagnetOwner? Owner { get; private set; }

    internal MagnetDefinition? Definition { get; set; }

    /// <summary>
    /// Index assigned by the compiler (valid only after compilation).
    /// </summary>
    internal int Index { get; set; } = -1;

    internal void Attach(IMagnetOwner owner)
    {
        if (Owner is not null && !ReferenceEquals(Owner, owner))
        {
            throw new InvalidOperationException(
                $"Magnet node '{MagnetId}' already belongs to another Magnet layout: definitions and nodes cannot be shared across layouts."
            );
        }

        Owner = owner;
    }

    internal void Detach() => Owner = null;

    /// <summary>
    /// Notifies the owner about a change.
    /// </summary>
    protected void Notify(MagnetChange change)
    {
        if (change != MagnetChange.None)
        {
            Owner?.OnNodeChanged(this, change);
        }
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged" />.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets a field whose change requires a recompilation.
    /// </summary>
    private protected bool SetStructure<T>(ref T field, T value, uint setBit = 0, [CallerMemberName] string? propertyName = null)
        => Set(ref field, value, MagnetChange.Structure, setBit, propertyName);

    /// <summary>
    /// Sets a field whose change only patches values.
    /// </summary>
    private protected bool SetValues<T>(ref T field, T value, uint setBit = 0, [CallerMemberName] string? propertyName = null)
        => Set(ref field, value, MagnetChange.Values, setBit, propertyName);

    private bool Set<T>(ref T field, T value, MagnetChange change, uint setBit, string? propertyName)
    {
        MarkSet(setBit);

        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        Notify(change);

        return true;
    }

    /// <summary>
    /// Creates an observable list which raises <see cref="MagnetChange.Structure" /> on every change.
    /// </summary>
    private protected ObservableCollection<T> CreateStructureList<T>()
    {
        var list = new ObservableCollection<T>();
        list.CollectionChanged += OnStructureListChanged;

        return list;
    }

    /// <summary>
    /// Creates an observable list which raises <see cref="MagnetChange.Values" /> on every change.
    /// </summary>
    private protected ObservableCollection<T> CreateValuesList<T>()
    {
        var list = new ObservableCollection<T>();
        list.CollectionChanged += OnValuesListChanged;

        return list;
    }

    /// <summary>
    /// Replaces the contents of <paramref name="list" /> with <paramref name="items" />: the backing list (and its
    /// change subscription) never changes identity, so XAML attribute assignment and element content behave the same.
    /// </summary>
    private protected static void ReplaceListContents<T>(IList<T> list, IEnumerable<T>? items)
    {
        if (ReferenceEquals(list, items))
        {
            return;
        }

        list.Clear();

        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            list.Add(item);
        }
    }

    private void OnStructureListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Notify(MagnetChange.Structure);

    private void OnValuesListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Notify(MagnetChange.Values);

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name}('{MagnetId}')";
}
