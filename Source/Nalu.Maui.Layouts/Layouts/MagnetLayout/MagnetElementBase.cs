using System.Diagnostics;
using Nalu.Cassowary;
using Nalu.Internals;

namespace Nalu.MagnetLayout;

/// <summary>
/// Delegate for creating constraints.
/// </summary>
public delegate IEnumerable<Constraint> ConstraintsFactory(IMagnetStage stage);

static file class MagnetElementIds
{
    public static int IdCounter;
}

/// <summary>
/// Base class for elements that can be part of a magnet layout stage.
/// </summary>
/// <typeparam name="TConstraintType">Enum type defining all the possible constraints applied by the element.</typeparam>
public abstract class MagnetElementBase<TConstraintType> : BindableObject, IMagnetElement
    where TConstraintType : struct, Enum
{
    private class StoredConstraintsFactory
    {
        public bool IsSet { get; set; }
        public ConstraintsFactory? CreateConstraints { get; set; }
    }
    
    /// <summary>
    /// Bindable property for <see cref="Id" />.
    /// </summary>
    public static readonly BindableProperty IdProperty = BindableProperty.Create(
        nameof(Id),
        typeof(string),
        typeof(MagnetElementBase<TConstraintType>),
        defaultValueCreator: bindable => $"{bindable.GetType().Name}-{++MagnetElementIds.IdCounter}",
        defaultBindingMode: BindingMode.OneTime,
        propertyChanged: OnIdPropertyChanged
    );

    private readonly SealedEnumDictionary<TConstraintType, StoredConstraintsFactory> _constraintsFactories = new(_ => new StoredConstraintsFactory());
    private readonly SealedEnumDictionary<TConstraintType, List<Constraint>?> _constraints = new();
    private bool _applyConstraintsImmediately;
    private IMagnetStage? _stage;

    /// <summary>
    /// Gets or sets the unique identifier for this element.
    /// </summary>
    public string Id
    {
        get => (string?) GetValue(IdProperty) ?? throw new NullReferenceException("Id cannot be null");
        set => SetValue(IdProperty, value ?? throw new ArgumentNullException(nameof(value), "Id cannot be null"));
    }

    /// <inheritdoc />
    public void SetStage(IMagnetStage? stage)
    {
        if (_stage != null)
        {
            foreach (var (type, storedFactory) in _constraintsFactories)
            {
                if (!storedFactory.IsSet)
                {
                    continue;
                }

                storedFactory.IsSet = false;

                if (_constraints[type] is { } constraints)
                {
                    foreach (var constraint in constraints)
                    {
                        _stage.RemoveConstraint(constraint);
                    }
                }
            }

            foreach (var (variable, _) in GetEditableVariables())
            {
                _stage.RemoveEditVariable(variable);
            }
        }

        _stage = stage;

        if (_stage is not null)
        {
            foreach (var (variable, strength) in GetEditableVariables())
            {
                _stage.AddEditVariable(variable, strength);
            }
        }
    }

    /// <inheritdoc />
    public void DetectChanges() => DetectChanges(_stage ?? throw new InvalidOperationException("Stage is not set"));

    /// <inheritdoc cref="IMagnetElement.DetectChanges" />
    protected virtual void DetectChanges(IMagnetStage stage)
    {
        // No-op by default
    }

    /// <summary>
    /// Sets the <paramref name="constraintsFactory" /> for the specified <paramref name="constraintType" /> and invalidates the stage
    /// only if the <see cref="ConstraintsFactory" /> on the <paramref name="constraintType" /> is not already set to the same value.
    /// </summary>
    protected void EnsureConstraintsFactory(TConstraintType constraintType, ConstraintsFactory constraintsFactory)
    {
        var factory = _constraintsFactories[constraintType];

        if (factory.CreateConstraints is null || factory.CreateConstraints != constraintsFactory)
        {
            factory.CreateConstraints = constraintsFactory;
            factory.IsSet = false;
            _stage?.Invalidate();
        }
    }

    /// <summary>
    /// Sets the <paramref name="constraintsFactory" /> for the specified <paramref name="constraintType" /> and invalidates the stage.
    /// </summary>
    protected void UpdateConstraints(TConstraintType constraintType, ConstraintsFactory? constraintsFactory)
    {
        var storedFactory = _constraintsFactories[constraintType];

        if (constraintsFactory is null && storedFactory.CreateConstraints is null)
        {
            // Both are null, no need to invalidate the stage or do anything
            return;
        }

        storedFactory.CreateConstraints = constraintsFactory;

        if (_applyConstraintsImmediately)
        {
            ApplyConstraints(constraintType, constraintsFactory);
            storedFactory.IsSet = true;
        }
        else
        {
            storedFactory.IsSet = false;
            _stage?.Invalidate();
        }
    }

    /// <inheritdoc />
    public void ApplyConstraints() => ApplyConstraints(_stage ?? throw new InvalidOperationException("Stage is not set"));

    /// <inheritdoc />
    public void FinalizeConstraints()
    {
        _applyConstraintsImmediately = true;
        FinalizeConstraints(_stage ?? throw new InvalidOperationException("Stage is not set"));
        _applyConstraintsImmediately = false;
    }

    /// <inheritdoc cref="IMagnetElement.FinalizeConstraints"/>
    protected virtual void FinalizeConstraints(IMagnetStage stage)
    {
    }

    /// <summary>
    /// Gets the editable variables for the element.
    /// </summary>
    /// <returns></returns>
    protected virtual (Variable Variable, double Strength)[] GetEditableVariables() => [];

    /// <summary>
    /// Applies the constraints for the element.
    /// </summary>
    protected virtual void ApplyConstraints(IMagnetStage stage) => ApplyChangedConstraints();

    private void ApplyChangedConstraints()
    {
        foreach (var (type, storedFactory) in _constraintsFactories)
        {
            if (storedFactory.IsSet)
            {
                continue;
            }

            ApplyConstraints(type, storedFactory.CreateConstraints);
            storedFactory.IsSet = true;
        }
    }

    /// <summary>
    /// Invalidates the stage (if any).
    /// </summary>
    protected void InvalidateStage() => _stage?.Invalidate();

    private void ApplyConstraints(TConstraintType constraintType, ConstraintsFactory? constraintsFactory)
    {
        var stage = _stage ?? throw new InvalidOperationException("Stage is not set");

        var appliedConstraints = _constraints[constraintType];
        if (appliedConstraints is not null)
        {
            foreach (var appliedConstraint in appliedConstraints)
            {
                stage.RemoveConstraint(appliedConstraint);
            }
        }

        if (constraintsFactory is null)
        {
            return;
        }

        if (appliedConstraints is null)
        {
            appliedConstraints = [..constraintsFactory(stage)];
            _constraints[constraintType] = appliedConstraints;
        }
        else
        {
            appliedConstraints.Clear();
            appliedConstraints.AddRange(constraintsFactory(stage));
        }

        foreach (var constraint in appliedConstraints)
        {
            stage.AddConstraint(constraint);
        }
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
        var storedFactory = _constraintsFactories[constraintType];
        if (storedFactory.CreateConstraints is null)
        {
            return;
        }

        storedFactory.CreateConstraints = null;

        if (_applyConstraintsImmediately)
        {
            ApplyConstraints(constraintType, null);
            storedFactory.IsSet = true;
        }
        else
        {
            storedFactory.IsSet = false;
            _stage?.Invalidate();
        }
    }

    private static void OnIdPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue == null)
        {
            throw new ArgumentNullException(nameof(newValue), "Id cannot be null");
        }

        if (bindable is MagnetElementBase<TConstraintType> magnetElement)
        {
            if (magnetElement._stage != null)
            {
                throw new InvalidOperationException("Id cannot be changed once the element is added to a stage");
            }

            if (Debugger.IsAttached)
            {
                magnetElement.SetVariableNames((string) newValue);
            }
        }
    }
}
