using System.Collections.ObjectModel;
using System.Collections.Specialized;

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
public abstract class MagnetNode : BindableObject
{
    /// <summary>
    /// Bindable property for <see cref="MagnetId" />.
    /// </summary>
    public static readonly BindableProperty MagnetIdProperty = BindableProperty.Create(
        nameof(MagnetId),
        typeof(string),
        typeof(MagnetNode),
        propertyChanged: (b, o, n) => ((MagnetNode) b).OnMagnetIdChanged((string?) o, (string?) n)
    );

    /// <summary>
    /// Gets or sets the identifier of this node. Required and unique within a <see cref="MagnetDefinition" />.
    /// </summary>
    /// <remarks>
    /// This is the only identity of a node: nothing falls back to <c>AutomationId</c>. It should not be changed after registration.
    /// </remarks>
    public string? MagnetId
    {
        get => (string?) GetValue(MagnetIdProperty);
        set => SetValue(MagnetIdProperty, value);
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
    /// Bindable property changed handler for properties whose change requires a recompilation.
    /// </summary>
#pragma warning disable IDE0060
    protected static void OnStructurePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        => ((MagnetNode) bindable).Notify(MagnetChange.Structure);

    /// <summary>
    /// Bindable property changed handler for properties whose change only patches values.
    /// </summary>
    protected static void OnValuePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        => ((MagnetNode) bindable).Notify(MagnetChange.Values);
#pragma warning restore IDE0060

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

    private void OnMagnetIdChanged(string? oldValue, string? newValue)
    {
        Definition?.OnNodeIdChanged(this, oldValue, newValue);
        Notify(MagnetChange.Structure);
    }

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name}('{MagnetId}')";
}
