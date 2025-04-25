using System.Runtime.InteropServices;
using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// Delegate for creating constraints.
/// </summary>
public delegate IEnumerable<Constraint> ConstraintsFactory(IMagnetStage stage);

/// <summary>
/// Base class for elements that can be part of a magnet layout stage.
/// </summary>
/// <typeparam name="TConstraintType">Enum type defining all the possible constraints applied by the element.</typeparam>
public abstract class MagnetElementBase<TConstraintType> : BindableObject, IMagnetElement
    where TConstraintType : notnull
{
    /// <summary>
    /// Bindable property for <see cref="Id"/>.
    /// </summary>
    public static readonly BindableProperty IdProperty = BindableProperty.Create(
        nameof(Id),
        typeof(string),
        typeof(MagnetElementBase<TConstraintType>),
        defaultValue: null,
        defaultBindingMode: BindingMode.OneTime,
        propertyChanged: OnIdPropertyChanged);

    private readonly Dictionary<TConstraintType, ConstraintsFactory> _constraintsFactories = [];
    private readonly Dictionary<TConstraintType, List<Constraint>> _constraints = [];
    private IMagnetStage? _stage;

    /// <summary>
    /// Gets or sets the unique identifier for this element.
    /// </summary>
    public string Id
    {
        get => (string?)GetValue(IdProperty) ?? throw new NullReferenceException("Id cannot be null");
        set => SetValue(IdProperty, value ?? throw new ArgumentNullException(nameof(value), "Id cannot be null"));
    }

    /// <inheritdoc />
    public void SetStage(IMagnetStage? stage)
    {
        if (_stage != null)
        {
            RemoveConstraints();
        }

        _stage = stage;

        if (_stage != null)
        {
            AddConstraints();
        }
    }

    /// <summary>
    /// Sets the <paramref name="constraintsFactory"/> for the specified <paramref name="constraintType"/>
    /// only if the <see cref="ConstraintsFactory"/> on the <paramref name="constraintType"/> is not already set.
    /// </summary>
    protected void TryAddConstraints(TConstraintType constraintType, ConstraintsFactory constraintsFactory)
    {
        if (_constraintsFactories.TryAdd(constraintType, constraintsFactory))
        {
            ApplyConstraints(constraintType, constraintsFactory);
        }
    }

    /// <summary>
    /// Sets the <paramref name="constraintsFactory"/> for the specified <paramref name="constraintType"/>.
    /// </summary>
    /// <remarks>
    /// Removes the existing constraints of the same type if they exist.
    /// Immediately applies the new constraints to the stage if it is already set.
    /// </remarks>
    protected void SetConstraints(TConstraintType constraintType, ConstraintsFactory constraintsFactory)
    {
        _constraintsFactories[constraintType] = constraintsFactory;

        ApplyConstraints(constraintType, constraintsFactory);
    }

    private void ApplyConstraints(TConstraintType constraintType, ConstraintsFactory constraintsFactory)
    {
        if (_stage is null)
        {
            return;
        }
        
        ref var appliedConstraints = ref CollectionsMarshal.GetValueRefOrAddDefault(_constraints, constraintType, out var exists);

        if (exists)
        {
            foreach (var appliedConstraint in appliedConstraints!)
            {
                _stage.RemoveConstraint(appliedConstraint);
            }
        }
        
        appliedConstraints = [..constraintsFactory(_stage)];

        foreach (var constraint in appliedConstraints)
        {
            _stage.AddConstraint(constraint);
        }

        _stage.Invalidate();
    }

    /// <summary>
    /// Initializes the variables for the element given the chosen element identifier.
    /// </summary>
    protected abstract void SetVariableNames(string id);

    /// <summary>
    /// Removes all constraints of the specified type.
    /// </summary>
    protected void RemoveConstraints(TConstraintType constraintType)
    {
        _constraintsFactories.Remove(constraintType);

        if (_stage is not null && _constraints.Remove(constraintType, out var constraints))
        {
            foreach (var constraint in constraints)
            {
                _stage.RemoveConstraint(constraint);
            }
        }
    }

    private void AddConstraints()
    {
        var stage = _stage!;
        foreach (var (constraintType, constraintFactory) in _constraintsFactories)
        {
            List<Constraint> constraints = [..constraintFactory(stage)];
            _constraints[constraintType] = constraints;

            foreach (var constraint in constraints)
            {
                stage.AddConstraint(constraint);
            }
        }
    }

    private void RemoveConstraints()
    {
        foreach (var constraint in _constraints.Values.SelectMany(c => c))
        {
            _stage!.RemoveConstraint(constraint);
        }

        _constraints.Clear();
    }

    private static void OnIdPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue == null)
        {
            throw new ArgumentNullException(nameof(newValue), "Id cannot be null");
        }

        if (oldValue != null)
        {
            throw new InvalidOperationException("Id cannot be changed once it has been set.");
        }

        if (bindable is MagnetElementBase<TConstraintType> magnetElement)
        {
            magnetElement.SetVariableNames((string)newValue);
        }
    }
}
