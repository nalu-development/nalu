namespace Nalu;

using System.Collections;
using Cassowary;

/// <summary>
/// Describe the scene for the ConstraintLayout.
/// </summary>
public class ConstraintLayoutScene : IConstraintLayoutScene, IList<ISceneElement>
{
    private readonly Dictionary<string, ISceneElement> _elements = [];
    private Constraint? _leftConstraint;
    private Constraint? _rightConstraint;
    private Constraint? _topConstraint;
    private Constraint? _bottomConstraint;
    private WeakReference<IConstraintLayout>? _weakLayout;

    /// <inheritdoc />
    public string Id => "parent";

    /// <inheritdoc />
    public Variable Left { get; } = new();

    /// <inheritdoc />
    public Variable Right { get; } = new();

    /// <inheritdoc />
    public Variable Top { get; } = new();

    /// <inheritdoc />
    public Variable Bottom { get; } = new();

    /// <inheritdoc />
    public Solver Solver { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstraintLayoutScene"/> class.
    /// </summary>
    public ConstraintLayoutScene()
    {
        Left.SetName("parent.Left");
        Right.SetName("parent.Right");
        Top.SetName("parent.Top");
        Bottom.SetName("parent.Bottom");

        ApplyCoordinate(ref _leftConstraint, 0, Left, true);
        ApplyCoordinate(ref _topConstraint, 0, Top, true);
        ApplyCoordinate(ref _rightConstraint, 0, Right, true);
        ApplyCoordinate(ref _bottomConstraint, 0, Bottom, true);
    }

    /// <inheritdoc />
    public IView? GetView(string id)
        => _weakLayout?.TryGetTarget(out var layout) == true ? layout.GetView(id) : null;

    /// <inheritdoc />
    public ISceneElementBase? GetElement(string id)
    {
        if (id == "parent")
        {
            return this;
        }

        return _elements.GetValueOrDefault(id);
    }

    /// <inheritdoc />
    public void InvalidateScene()
    {
        if (_weakLayout?.TryGetTarget(out var layout) == true)
        {
            layout.InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    public void Apply(double left, double top, double right, double bottom)
    {
        ApplyCoordinate(ref _leftConstraint, left, Left);
        ApplyCoordinate(ref _topConstraint, top, Top);
        ApplyCoordinate(ref _rightConstraint, right, Right);
        ApplyCoordinate(ref _bottomConstraint, bottom, Bottom);

        if (_weakLayout?.TryGetTarget(out _) == true)
        {
            foreach (var element in _elements.Values)
            {
                element.ApplyConstraints();
            }

            foreach (var element in _elements.Values)
            {
                element.ApplyConstraints();
            }
        }

        foreach (var (variable, value) in Solver.FetchChanges())
        {
            variable.Value = value;
        }
    }

    /// <inheritdoc />
    public void SetLayout(IConstraintLayout? layout)
    {
        if (_weakLayout?.TryGetTarget(out var oldLayout) == true && oldLayout != layout)
        {
            foreach (var element in _elements.Values)
            {
                element.SetScene(null);
            }
        }

        if (layout == null)
        {
            _weakLayout = null;
            return;
        }

        _weakLayout = new(layout);
        foreach (var element in _elements.Values)
        {
            element.SetScene(this);
        }
    }

    #region IList<ISceneElement>

    /// <inheritdoc />
    public int Count => _elements.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public ISceneElement this[int index]
    {
        get => throw IndexAccessNotSupported();
        set => throw IndexAccessNotSupported();
    }

    /// <inheritdoc />
    public void Add(ISceneElement item)
    {
        _elements[item.Id] = item;
        if (_weakLayout?.TryGetTarget(out _) == true)
        {
            item.SetScene(this);
        }

        InvalidateScene();
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var element in _elements.Values)
        {
            element.SetScene(null);
        }

        _elements.Clear();
        InvalidateScene();
    }

    /// <inheritdoc />
    public bool Contains(ISceneElement item) => _elements.ContainsKey(item.Id);

    /// <inheritdoc />
    public void CopyTo(ISceneElement[] array, int arrayIndex) => _elements.Values.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<ISceneElement> GetEnumerator() => _elements.Values.GetEnumerator();

    /// <inheritdoc />
    public int IndexOf(ISceneElement item) => throw IndexAccessNotSupported();

    /// <inheritdoc />
    public void Insert(int index, ISceneElement item) => throw IndexAccessNotSupported();

    /// <inheritdoc />
    public bool Remove(ISceneElement item)
    {
        item.SetScene(null);
        var removed = _elements.Remove(item.Id);
        InvalidateScene();
        return removed;
    }

    /// <inheritdoc />
    public void RemoveAt(int index) => throw IndexAccessNotSupported();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    private void ApplyCoordinate(ref Constraint? coordinateConstraint, double newValue, Variable variable, bool force = false)
    {
        if (newValue != variable.Value || force)
        {
            variable.Value = newValue;
            if (coordinateConstraint.HasValue)
            {
                Solver.RemoveConstraint(coordinateConstraint.Value);
            }

            coordinateConstraint = variable | WeightedRelation.Eq(Strength.Required) | newValue;
            Solver.AddConstraint(coordinateConstraint.Value);
        }
    }

    private static NotSupportedException IndexAccessNotSupported() => new("Index access is not supported for this collection.");
}
