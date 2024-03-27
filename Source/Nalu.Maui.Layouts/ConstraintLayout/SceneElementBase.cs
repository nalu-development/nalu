namespace Nalu;

using Cassowary;

#pragma warning disable SA1402

/// <summary>
/// Represents a scene element.
/// </summary>
/// <typeparam name="TConstraint">Enum type defining all possible constraints applied by the scene element.</typeparam>
public abstract class SceneElementBase<TConstraint> : SceneElementBase
    where TConstraint : notnull
{
    private readonly Dictionary<TConstraint, Func<IConstraintLayoutScene, IEnumerable<Constraint>>> _delayedConstraints = new(4);
    private readonly Dictionary<TConstraint, IReadOnlyCollection<Constraint>> _appliedConstraints = new(4);
    private readonly Dictionary<TConstraint, IReadOnlyCollection<Constraint>?> _changedConstraints = new(4);

    /// <inheritdoc cref="ISceneElement.ApplyConstraints"/>
    public override void ApplyConstraints()
    {
        var scene = Scene;
        ArgumentNullException.ThrowIfNull(scene);

        if (_delayedConstraints.Count > 0)
        {
            foreach (var (type, constraintsFactory) in _delayedConstraints)
            {
                var constraints = constraintsFactory(scene);
                _changedConstraints[type] = constraints as IReadOnlyCollection<Constraint> ?? constraints.ToArray();
            }

            _delayedConstraints.Clear();
        }

        if (_changedConstraints.Count == 0)
        {
            return;
        }

        var solver = scene.Solver;
        foreach (var (type, constraints) in _changedConstraints)
        {
            if (_appliedConstraints.TryGetValue(type, out var oldConstraints) && oldConstraints.Count > 0)
            {
                foreach (var oldConstraint in oldConstraints)
                {
                    solver.RemoveConstraint(oldConstraint);
                }
            }

            if (constraints?.Count > 0)
            {
                _appliedConstraints[type] = constraints;
                foreach (var constraint in constraints)
                {
                    solver.AddConstraint(constraint);
                }
            }
            else
            {
                _appliedConstraints.Remove(type);
            }
        }

        _changedConstraints.Clear();
    }

    /// <inheritdoc />
    protected sealed override void DisposeFromScene()
    {
        var solver = Scene?.Solver;
        if (solver is not null)
        {
            foreach (var (type, constraints) in _appliedConstraints)
            {
                foreach (var constraint in constraints)
                {
                    solver.RemoveConstraint(constraint);
                }

                _changedConstraints.TryAdd(type, constraints);
            }
        }

        _appliedConstraints.Clear();
    }

    /// <summary>
    /// Sets a constraint for the next layout pass.
    /// </summary>
    /// <param name="type">Type of the constraint.</param>
    /// <param name="constraintsFactory">The constraint factory function.</param>
    protected void SetConstraint(TConstraint type, Func<IConstraintLayoutScene, IEnumerable<Constraint>> constraintsFactory)
    {
        if (Scene is { } scene)
        {
            var constraints = constraintsFactory(scene);
            _changedConstraints[type] = constraints as IReadOnlyCollection<Constraint> ?? constraints.ToArray();
        }
        else
        {
            _delayedConstraints[type] = constraintsFactory;
        }
    }
}

/// <summary>
/// Represents a scene element.
/// </summary>
public abstract class SceneElementBase : BindableObject, ISceneElement
{
    /// <inheritdoc cref="ISceneElementBase.Id"/>
    public static readonly BindableProperty IdProperty = BindableProperty.Create(nameof(Id), typeof(string), typeof(SceneElementBase), propertyChanged: IdPropertyChanged, defaultValueCreator: IdPropertyDefaultValueCreator);

    private readonly string _defaultId = Guid.NewGuid().ToString();

    /// <inheritdoc cref="ISceneElementBase.Id"/>
    public string Id
    {
        get => (string)GetValue(IdProperty);
        init => SetValue(IdProperty, value);
    }

    /// <inheritdoc cref="ISceneElementBase.Left"/>
    public Variable Left { get; } = new();

    /// <inheritdoc cref="ISceneElementBase.Right"/>
    public Variable Right { get; } = new();

    /// <inheritdoc cref="ISceneElementBase.Top"/>
    public Variable Top { get; } = new();

    /// <inheritdoc cref="ISceneElementBase.Bottom"/>
    public Variable Bottom { get; } = new();

    /// <summary>
    /// Gets the reference to current scene.
    /// </summary>
    protected IConstraintLayoutScene? Scene { get; private set; }

    /// <inheritdoc cref="ISceneElement.SetScene"/>
    public virtual void SetScene(IConstraintLayoutScene? scene)
    {
        DisposeFromScene();
        Scene = scene;
    }

    /// <inheritdoc cref="ISceneElement.ApplyConstraints"/>
    public abstract void ApplyConstraints();

    /// <summary>
    /// Removes all constraints from the scene.
    /// </summary>
    protected abstract void DisposeFromScene();

    /// <summary>
    /// Create variables for the scene element.
    /// </summary>
    /// <param name="id">The identifier.</param>
    protected virtual void SetVariablesNames(string id)
    {
        Left.SetName($"{id}.Left");
        Right.SetName($"{id}.Right");
        Top.SetName($"{id}.Top");
        Bottom.SetName($"{id}.Bottom");
    }

    private static object IdPropertyDefaultValueCreator(BindableObject bindable)
        => ((SceneElementBase)bindable)._defaultId;

    private static void IdPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        var sceneElement = (SceneElementBase)bindable;
        if ((string)oldvalue != sceneElement._defaultId)
        {
            throw new InvalidOperationException("Id cannot be changed once set");
        }

        var id = (string?)newvalue;
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (id.Contains('!'))
        {
            throw new ArgumentException("Id cannot contain '!' character");
        }

        sceneElement.SetVariablesNames(id);
    }
}
