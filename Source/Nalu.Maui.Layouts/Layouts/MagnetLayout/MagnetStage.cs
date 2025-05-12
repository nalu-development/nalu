using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// The magnet stage.
/// </summary>
public class MagnetStage : BindableObject, IMagnetStage, IList<IMagnetElement>
{
    private readonly Solver _solver = new();
    private readonly List<IMagnetElement> _elements = [];
    private IReadOnlyDictionary<string, IMagnetElementBase>? _elementById;

    /// <inheritdoc />
    public string Id => IMagnetStage.StageId;

    /// <inheritdoc />
    public Variable Top { get; } = new();

    /// <inheritdoc />
    public Variable Bottom { get; } = new();

    /// <inheritdoc />
    public Variable Left { get; } = new();

    /// <inheritdoc />
    public Variable Right { get; } = new();

    /// <inheritdoc />
    public int Count => _elements.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public IMagnetElement this[int index]
    {
        get => _elements[index];
        set
        {
            if (_elements[index] != value)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (value is not null)
                {
                    value.SetStage(this);
                    _elements[index] = value;
                }
                else
                {
                    throw new ArgumentNullException();
                }
            }
        }
    }

    private readonly Variable _lowBottom = new();
    private readonly Variable _lowRight = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MagnetStage" /> class.
    /// </summary>
    public MagnetStage()
    {
        if (Debugger.IsAttached)
        {
            Left.SetName("Stage.Left");
            Right.SetName("Stage.Right");
            Top.SetName("Stage.Top");
            Bottom.SetName("Stage.Bottom");
            _lowRight.SetName("Stage.LowRight");
            _lowBottom.SetName("Stage.LowBottom");
        }

        _solver.AddConstraints(
            Left | WeightedRelation.Eq(Strength.Required) | 0,
            Top | WeightedRelation.Eq(Strength.Required) | 0,
            Right | WeightedRelation.Eq(Strength.Required) | _lowRight,
            Bottom | WeightedRelation.Eq(Strength.Required) | _lowBottom
        );

        _solver.AddEditVariable(Bottom, Strength.Weak);
        _solver.AddEditVariable(Right, Strength.Weak);
        _solver.AddEditVariable(_lowBottom, Strength.Weak);
        _solver.AddEditVariable(_lowRight, Strength.Weak);
    }

    /// <inheritdoc />
    public void Add(IMagnetElement item)
    {
        _elements.Add(item);
        item.SetStage(this);
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var element in _elements)
        {
            element.SetStage(null);
        }

        _elements.Clear();
    }

    /// <inheritdoc />
    public bool Contains(IMagnetElement item) => _elements.Contains(item);

    /// <inheritdoc />
    public void CopyTo(IMagnetElement[] array, int arrayIndex) => _elements.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(IMagnetElement item) => _elements.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, IMagnetElement item)
    {
        _elements.Insert(index, item);
        item.SetStage(this);
    }

    /// <inheritdoc />
    public bool Remove(IMagnetElement item)
    {
        if (_elements.Remove(item))
        {
            item.SetStage(null);
            _elementById = null;

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        var item = _elements[index];
        item.SetStage(null);
        _elements.RemoveAt(index);
        _elementById = null;
    }

    /// <inheritdoc />
    public double WidthRequest { get; private set; }

    /// <inheritdoc />
    public double HeightRequest { get; private set; }

    /// <inheritdoc />
    public void Invalidate()
    {
        // No-op for now
    }

    /// <inheritdoc />
    public void AddConstraint(Constraint constraint) => _solver.AddConstraint(constraint);

    /// <inheritdoc />
    public void RemoveConstraint(Constraint constraint) => _solver.RemoveConstraint(constraint);

    /// <inheritdoc />
    public IMagnetElementBase GetElement(string identifier)
    {
        if (identifier == Id)
        {
            return this;
        }

        return GetElementsById()[identifier];
    }

    /// <inheritdoc />
    public bool TryGetElement(string identifier, [NotNullWhen(true)] out IMagnetElementBase? element)
    {
        if (identifier == Id)
        {
            element = this;

            return true;
        }

        return GetElementsById().TryGetValue(identifier, out element);
    }

    /// <inheritdoc />
    public void AddEditVariable(Variable variable, double strength) => _solver.AddEditVariable(variable, strength);

    /// <inheritdoc />
    public void RemoveEditVariable(Variable variable) => _solver.RemoveEditVariable(variable);

    /// <inheritdoc />
    public void SuggestValue(Variable variable, double value) => _solver.SuggestValue(variable, value);

    private void SetBounds(double width, double height, bool forMeasure)
    {
        WidthRequest = width;
        HeightRequest = height;

        if (double.IsPositiveInfinity(width))
        {
            width = 100_000;
        }

        if (double.IsPositiveInfinity(height))
        {
            height = 100_000;
        }

        _solver.SuggestValue(Right, width);
        _solver.SuggestValue(Bottom, height);

        if (forMeasure)
        {
            _solver.SuggestValue(_lowRight, 0);
            _solver.SuggestValue(_lowBottom, 0);
        }
        else
        {
            _solver.SuggestValue(_lowRight, width);
            _solver.SuggestValue(_lowBottom, height);
        }
    }

    /// <inheritdoc />
    public void PrepareForMeasure(double width, double height)
    {
        foreach (var element in _elements)
        {
            element.DetectChanges();
        }

        SetBounds(width, height, true);

        foreach (var element in _elements)
        {
            element.ApplyConstraints();
        }

        _solver.FetchChanges();

        foreach (var element in _elements)
        {
            element.FinalizeConstraints();
        }

        _solver.FetchChanges();
    }

    /// <inheritdoc />
    public void PrepareForArrange(double width, double height)
    {
        SetBounds(width, height, false);

        foreach (var element in _elements)
        {
            element.FinalizeConstraints();
        }

        _solver.FetchChanges();
    }

    /// <inheritdoc />
    public IEnumerator<IMagnetElement> GetEnumerator() => _elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IReadOnlyDictionary<string, IMagnetElementBase> GetElementsById()
        => _elementById ??= _elements.Cast<IMagnetElementBase>().ToFrozenDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
}
