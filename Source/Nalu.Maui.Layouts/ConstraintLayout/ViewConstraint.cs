namespace Nalu;

using Cassowary;
using static Cassowary.Strength;
using static Cassowary.WeightedRelation;
using AnchorConstraints = (Cassowary.Constraint AnchorConstraint, Cassowary.Expression Anchor);

// ReSharper disable CompareOfFloatsByEqualityOperator

/// <summary>
/// Represents a view in a ConstraintLayout.
/// This class is used to define a set of constraints for a view in a ConstraintLayout.
/// Each property corresponds to a possible constraint on the element's position in the layout.
/// </summary>
public class ViewConstraint : SceneElementBase<ViewConstraint.ConstraintTypes>, ISceneViewConstraint
{
#pragma warning disable CS1591,SA1602
    public enum ConstraintTypes
    {
        Horizontal,
        Width,
        Vertical,
        Height,
        Measure,
        Visibility,
    }
#pragma warning restore CS1591,SA1602

    /// <summary>
    /// Identifies the TopToTopOf bindable property.
    /// This property is used to align the top edge of the element with the top edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty TopToTopOfProperty = BindableProperty.Create(
        nameof(TopToTopOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Top;
                anchor.TargetFunc = e => e.Top;
                anchor.Type = AnchorType.TopToTopOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._topAnchor = newValue as Anchor ?? viewConstraint.TopToBottomOf;
            viewConstraint.NotifyVerticalPropertyChanged();
        });

    /// <summary>
    /// Identifies the LeftToLeftOf bindable property.
    /// This property is used to align the left edge of the element with the left edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty LeftToLeftOfProperty = BindableProperty.Create(
        nameof(LeftToLeftOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Left;
                anchor.TargetFunc = e => e.Left;
                anchor.Type = AnchorType.LeftToLeftOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._leftAnchor = newValue as Anchor ?? viewConstraint.LeftToRightOf;
            viewConstraint.NotifyHorizontalPropertyChanged();
        });

    /// <summary>
    /// Identifies the TopToBottomOf bindable property.
    /// This property is used to align the top edge of the element with the bottom edge of another element.
    /// </summary>
    public static readonly BindableProperty TopToBottomOfProperty = BindableProperty.Create(
        nameof(TopToBottomOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Top;
                anchor.TargetFunc = e => e.Bottom;
                anchor.Type = AnchorType.TopToBottomOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._topAnchor = newValue as Anchor ?? viewConstraint.TopToTopOf;
            viewConstraint.NotifyVerticalPropertyChanged();
        });

    /// <summary>
    /// Identifies the LeftToRightOf bindable property.
    /// This property is used to align the left edge of the element with the right edge of another element.
    /// </summary>
    public static readonly BindableProperty LeftToRightOfProperty = BindableProperty.Create(
        nameof(LeftToRightOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Left;
                anchor.TargetFunc = e => e.Right;
                anchor.Type = AnchorType.LeftToRightOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._leftAnchor = newValue as Anchor ?? viewConstraint.LeftToLeftOf;
            viewConstraint.NotifyHorizontalPropertyChanged();
        });

    /// <summary>
    /// Identifies the RightToRightOf bindable property.
    /// This property is used to align the right edge of the element with the right edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty RightToRightOfProperty = BindableProperty.Create(
        nameof(RightToRightOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Right;
                anchor.TargetFunc = e => e.Right;
                anchor.Type = AnchorType.RightToRightOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._rightAnchor = newValue as Anchor ?? viewConstraint.RightToLeftOf;
            viewConstraint.NotifyHorizontalPropertyChanged();
        });

    /// <summary>
    /// Identifies the RightToLeftOf bindable property.
    /// This property is used to align the right edge of the element with the left edge of another element.
    /// </summary>
    public static readonly BindableProperty RightToLeftOfProperty = BindableProperty.Create(
        nameof(RightToLeftOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Right;
                anchor.TargetFunc = e => e.Left;
                anchor.Type = AnchorType.RightToLeftOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._rightAnchor = newValue as Anchor ?? viewConstraint.RightToRightOf;
            viewConstraint.NotifyHorizontalPropertyChanged();
        });

    /// <summary>
    /// Identifies the BottomToTopOf bindable property.
    /// This property is used to align the bottom edge of the element with the top edge of another element.
    /// </summary>
    public static readonly BindableProperty BottomToTopOfProperty = BindableProperty.Create(
        nameof(BottomToTopOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Bottom;
                anchor.TargetFunc = e => e.Top;
                anchor.Type = AnchorType.BottomToTopOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._bottomAnchor = newValue as Anchor ?? viewConstraint.BottomToBottomOf;
            viewConstraint.NotifyVerticalPropertyChanged();
        });

    /// <summary>
    /// Identifies the BottomToBottomOf bindable property.
    /// This property is used to align the bottom edge of the element with the bottom edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty BottomToBottomOfProperty = BindableProperty.Create(
        nameof(BottomToBottomOf),
        typeof(Anchor),
        typeof(ViewConstraint),
        coerceValue: (_, value) =>
        {
            if (value is Anchor anchor)
            {
                anchor.SourceFunc = e => e.Bottom;
                anchor.TargetFunc = e => e.Bottom;
                anchor.Type = AnchorType.BottomToBottomOf;
            }

            return value;
        },
        propertyChanged: (bindable, _, newValue) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint._bottomAnchor = newValue as Anchor ?? viewConstraint.BottomToTopOf;
            viewConstraint.NotifyVerticalPropertyChanged();
        });

    /// <summary>
    /// Identifies the Height bindable property.
    /// </summary>
    public static readonly BindableProperty HeightProperty = BindableProperty.Create(
        nameof(Height),
        typeof(SizeDefinition),
        typeof(ViewConstraint),
        SizeDefinition.Measured,
        propertyChanged: (bindable, _, _) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint.NotifyHeightPropertyChanged();
        });

    /// <summary>
    /// Identifies the Width bindable property.
    /// </summary>
    public static readonly BindableProperty WidthProperty = BindableProperty.Create(
        nameof(Width),
        typeof(SizeDefinition),
        typeof(ViewConstraint),
        SizeDefinition.Measured,
        propertyChanged: (bindable, _, _) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint.NotifyWidthPropertyChanged();
        });

    /// <summary>
    /// Identifies the HorizontalBias bindable property.
    /// </summary>
    public static readonly BindableProperty HorizontalBiasProperty = BindableProperty.Create(
        nameof(HorizontalBias),
        typeof(double),
        typeof(ViewConstraint),
        0.5d,
        propertyChanged: (bindable, _, _) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint.NotifyHorizontalPropertyChanged();
        });

    /// <summary>
    /// Identifies the VerticalBias bindable property.
    /// </summary>
    public static readonly BindableProperty VerticalBiasProperty = BindableProperty.Create(
        nameof(VerticalBias),
        typeof(double),
        typeof(ViewConstraint),
        0.5d,
        propertyChanged: (bindable, _, _) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint.NotifyVerticalPropertyChanged();
        });

    /// <summary>
    /// Identifies the IsVisible bindable property.
    /// This property is used internally to track the visibility of the target view.
    /// </summary>
    private static readonly BindableProperty _isVisibleProperty = BindableProperty.Create(
        nameof(IsVisible),
        typeof(bool),
        typeof(ViewConstraint),
        true,
        propertyChanged: (bindable, _, _) =>
        {
            var viewConstraint = (ViewConstraint)bindable;
            viewConstraint.NotifyVisibilityChanged();
        });

    private readonly Variable _measuredWidth = new();
    private readonly Variable _measuredHeight = new();
    private readonly Variable _chainWeightedWidth = new();
    private readonly Variable _chainWeightedHeight = new();
    private readonly Variable _leftAnchorVariable = new();
    private readonly Variable _rightAnchorVariable = new();
    private readonly Variable _topAnchorVariable = new();
    private readonly Variable _bottomAnchorVariable = new();
    private readonly Variable _visibilityMultiplier = new();

    private Anchor? _topAnchor;
    private Anchor? _leftAnchor;
    private Anchor? _rightAnchor;
    private Anchor? _bottomAnchor;

    /// <summary>
    /// Gets or sets the vertical bias of the element when both top and bottom constraints are set.
    /// </summary>
    public double VerticalBias
    {
        get => (double)GetValue(VerticalBiasProperty);
        set => SetValue(VerticalBiasProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal bias of the element when both left and right constraints are set.
    /// </summary>
    public double HorizontalBias
    {
        get => (double)GetValue(HorizontalBiasProperty);
        set => SetValue(HorizontalBiasProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the element.
    /// </summary>
    public SizeDefinition Height
    {
        get => (SizeDefinition)GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the element.
    /// </summary>
    public SizeDefinition Width
    {
        get => (SizeDefinition)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the top edge with.
    /// </summary>
    public Anchor? TopToTopOf
    {
        get => (Anchor?)GetValue(TopToTopOfProperty);
        set => SetValue(TopToTopOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the left edge with.
    /// </summary>
    public Anchor? LeftToLeftOf
    {
        get => (Anchor?)GetValue(LeftToLeftOfProperty);
        set => SetValue(LeftToLeftOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the top edge with its bottom edge.
    /// </summary>
    public Anchor? TopToBottomOf
    {
        get => (Anchor?)GetValue(TopToBottomOfProperty);
        set => SetValue(TopToBottomOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the left edge with its right edge.
    /// </summary>
    public Anchor? LeftToRightOf
    {
        get => (Anchor?)GetValue(LeftToRightOfProperty);
        set => SetValue(LeftToRightOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the right edge with.
    /// </summary>
    public Anchor? RightToRightOf
    {
        get => (Anchor?)GetValue(RightToRightOfProperty);
        set => SetValue(RightToRightOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the right edge with its left edge.
    /// </summary>
    public Anchor? RightToLeftOf
    {
        get => (Anchor?)GetValue(RightToLeftOfProperty);
        set => SetValue(RightToLeftOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the bottom edge with its top edge.
    /// </summary>
    public Anchor? BottomToTopOf
    {
        get => (Anchor?)GetValue(BottomToTopOfProperty);
        set => SetValue(BottomToTopOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the bottom edge with.
    /// </summary>
    public Anchor? BottomToBottomOf
    {
        get => (Anchor?)GetValue(BottomToBottomOfProperty);
        set => SetValue(BottomToBottomOfProperty, value);
    }

    private bool IsVisible
    {
        get => (bool)GetValue(_isVisibleProperty);
        set => SetValue(_isVisibleProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewConstraint"/> class.
    /// </summary>
    public ViewConstraint()
    {
        NotifyVisibilityChanged();
    }

    /// <inheritdoc />
    public void FinalizeConstraints()
    {
        var constraintLayoutScene = Scene;
        ArgumentNullException.ThrowIfNull(constraintLayoutScene);

        var view = constraintLayoutScene.GetView(Id);
        if (view is null)
        {
            return;
        }

        if ((Width.Unit != SizeUnit.Measured && Height.Unit != SizeUnit.Measured) ||
            (Width.Unit == SizeUnit.Measured && Height.Unit == SizeUnit.Measured))
        {
            return;
        }

        SetConstraint(ConstraintTypes.Measure, scene =>
        {
            var constrainedWidth = double.IsPositiveInfinity(scene.Right.CurrentValue) ? double.PositiveInfinity : scene.Right.CurrentValue - scene.Left.CurrentValue;
            var constrainedHeight = double.IsPositiveInfinity(scene.Bottom.CurrentValue) ? double.PositiveInfinity : scene.Bottom.CurrentValue - scene.Top.CurrentValue;

            if (Width.Unit != SizeUnit.Measured)
            {
                constrainedWidth = Right.CurrentValue - Left.CurrentValue;
            }

            if (Height.Unit != SizeUnit.Measured)
            {
                constrainedHeight = Bottom.CurrentValue - Top.CurrentValue;
            }

            var measured = view.Measure(constrainedWidth, constrainedHeight);

            return [
                _measuredWidth | Eq(Required) | measured.Width,
                _measuredHeight | Eq(Required) | measured.Height,
            ];
        });

        base.ApplyConstraints();
    }

    /// <summary>
    /// Applies view constraints to the scene solver.
    /// </summary>
    public override void ApplyConstraints()
    {
        var constraintLayoutScene = Scene;
        ArgumentNullException.ThrowIfNull(constraintLayoutScene);

        var view = constraintLayoutScene.GetView(Id);

        if (view is null)
        {
            DisposeFromScene();
            return;
        }

        IsVisible = view.Visibility != Visibility.Collapsed;
        SetConstraint(ConstraintTypes.Measure, _ =>
        {
            if (Width.Unit != SizeUnit.Measured && Height.Unit != SizeUnit.Measured)
            {
                return [];
            }

            var measured = view.Measure(double.PositiveInfinity, double.PositiveInfinity);

            return [
                _measuredWidth | Eq(Required) | measured.Width,
                _measuredHeight | Eq(Required) | measured.Height,
            ];
        });

        base.ApplyConstraints();
    }

    /// <inheritdoc/>
    protected override void SetVariablesNames(string id)
    {
        base.SetVariablesNames(id);
        _measuredHeight.SetName($"{id}.MeasuredHeight");
        _measuredWidth.SetName($"{id}.MeasuredWidth");
        _leftAnchorVariable.SetName($"{id}.LeftAnchor");
        _rightAnchorVariable.SetName($"{id}.RightAnchor");
        _topAnchorVariable.SetName($"{id}.TopAnchor");
        _bottomAnchorVariable.SetName($"{id}.BottomAnchor");
        _chainWeightedWidth.SetName($"{id}.ChainWeightedWidth");
        _chainWeightedHeight.SetName($"{id}.ChainWeightedHeight");
        _visibilityMultiplier.SetName($"{id}.VisibilityMultiplier");
    }

    private IEnumerable<Constraint> GetWidthConstraints(IConstraintLayoutScene scene)
        => !IsVisible
            ? [(Right - Left) | Eq(Required) | 0]
            : GetSizeConstraints(Width, _measuredWidth, Left, Right, _chainWeightedWidth, scene.Left, scene.Right, Bottom - Top);

    private IEnumerable<Constraint> GetHeightConstraints(IConstraintLayoutScene scene)
        => !IsVisible
            ? [(Bottom - Top) | Eq(Required) | 0]
            : GetSizeConstraints(Height, _measuredHeight, Top, Bottom, _chainWeightedHeight, scene.Top, scene.Bottom, Right - Left);

    private IEnumerable<Constraint> GetHorizontalConstraints(IConstraintLayoutScene scene)
        => GetPositionConstraints(
            scene,
            v => v.CreateLeftAnchorConstraint(scene),
            v => v.CreateRightAnchorConstraint(scene),
            v => v._chainWeightedWidth,
            v => v._leftAnchor,
            v => v._rightAnchor,
            v => v.Left,
            v => v.Right,
            _leftAnchorVariable,
            _rightAnchorVariable,
            HorizontalBias,
            Width);

    private IEnumerable<Constraint> GetVerticalConstraints(IConstraintLayoutScene scene)
        => GetPositionConstraints(
            scene,
            v => v.CreateTopAnchorConstraint(scene),
            v => v.CreateBottomAnchorConstraint(scene),
            v => v._chainWeightedHeight,
            v => v._topAnchor,
            v => v._bottomAnchor,
            v => v.Top,
            v => v.Bottom,
            _topAnchorVariable,
            _bottomAnchorVariable,
            VerticalBias,
            Height);

    private IEnumerable<Constraint> GetPositionConstraints(
        IConstraintLayoutScene scene,
        Func<ViewConstraint, AnchorConstraints?> createStartAnchorConstraint,
        Func<ViewConstraint, AnchorConstraints?> createEndAnchorConstraint,
        Func<ViewConstraint, Variable> chainWeightedSize,
        Func<ViewConstraint, Anchor?> startAnchorGetter,
        Func<ViewConstraint, Anchor?> endAnchorGetter,
        Func<ViewConstraint, Variable> startGetter,
        Func<ViewConstraint, Variable> endGetter,
        Variable startAnchorVariable,
        Variable endAnchorVariable,
        double bias,
        SizeDefinition size)
    {
        var start = startGetter(this);
        var end = endGetter(this);
        var startAnchorConstraint = createStartAnchorConstraint(this);
        var endAnchorConstraint = createEndAnchorConstraint(this);
        var isChainHead = false;
        var isChain = false;
        if (startAnchorConstraint is not null)
        {
            var (startConstraint, startAnchor) = startAnchorConstraint.Value;
            yield return startConstraint;
            yield return startAnchorVariable | Eq(Required) | startAnchor;

            // Chain elements (except the head) should constrain their size to the chain weighted size variable found inside the head.
            var chainWeightedWidthConstraint = GetChainWeightedSizeConstraint(scene, chainWeightedSize, startAnchorGetter, endAnchorGetter);
            if (chainWeightedWidthConstraint is not null)
            {
                isChain = true;
                yield return chainWeightedWidthConstraint.Value;
            }
            else if (endAnchorConstraint?.Anchor is { } rightAnchor)
            {
                isChain = isChainHead = HasChainedEnd(scene, startAnchorGetter, endAnchorGetter);
                if (!isChainHead)
                {
                    yield return chainWeightedSize(this) | Eq(Required) | (rightAnchor - startAnchor);
                }
            }
        }

        var hasVariableLeftConstraint = startAnchorConstraint?.AnchorConstraint is { Strength: < Required };
        var hasVariableRightConstraint = endAnchorConstraint?.AnchorConstraint is { Strength: < Required };
        if (hasVariableLeftConstraint && hasVariableRightConstraint && !(isChain && size.Unit == SizeUnit.Constraint))
        {
            var startAnchor = startAnchorConstraint!.Value.Anchor;
            var endAnchor = endAnchorConstraint!.Value.Anchor;
            yield return bias switch
            {
                1d => end | Eq(Required) | endAnchor,
                0d => start | Eq(Required) | startAnchor,

                // start = startAnchor + (endAnchor - startAnchor - size) * bias
                // start = startAnchor + (endAnchor - startAnchor - end + start) * bias
                // 0 = startAnchor + (endAnchor - startAnchor - end + start) * bias - start
                // 0 = startAnchor + bias * endAnchor - bias * startAnchor - bias * end + bias * start - start
                // 0 = startAnchor - bias * startAnchor + bias * endAnchor - bias * end + bias * start - start
                // 0 = startAnchor * (1 - bias) + bias * endAnchor - bias * end + start * (bias - 1)
                // bias * end = startAnchor * (1 - bias) + bias * endAnchor + start * (bias - 1)
                _ => (bias * end) | Eq(Required) | ((startAnchor * (1 - bias)) + (endAnchor * bias) + (start * (bias - 1))),
            };
        }
        else if (isChainHead && hasVariableLeftConstraint && !hasVariableRightConstraint)
        {
            var startAnchor = startAnchorConstraint!.Value.Anchor;
            var tail = GetChainTail(scene, startAnchorGetter, endAnchorGetter);
            var tailStartAnchorConstraint = createStartAnchorConstraint(tail);
            var tailEndAnchorConstraint = createEndAnchorConstraint(tail);
            var tailHasVariableStartConstraint = tailStartAnchorConstraint?.AnchorConstraint is { Strength: < Required };
            var tailHasVariableEndConstraint = tailEndAnchorConstraint?.AnchorConstraint is { Strength: < Required };
            if (tailEndAnchorConstraint is not null && tailHasVariableEndConstraint && !tailHasVariableStartConstraint && size.Unit != SizeUnit.Constraint)
            {
                var tailRightAnchor = tailEndAnchorConstraint.Value.Anchor;
                yield return (bias * endGetter(tail)) | Eq(Required) | ((startAnchor * (1 - bias)) + (tailRightAnchor * bias) + (start * (bias - 1)));
            }
        }

        if (endAnchorConstraint is not null)
        {
            var (endConstraint, endAnchor) = endAnchorConstraint.Value;
            yield return endConstraint;
            yield return endAnchorVariable | Eq(Required) | endAnchor;
        }
    }

    private bool HasChainedEnd(IConstraintLayoutScene scene, Func<ViewConstraint, Anchor?> startAnchorGetter, Func<ViewConstraint, Anchor?> endAnchorGetter)
    {
        var view = this;
        var endAnchor = endAnchorGetter(view);
        if (endAnchor is null || !endAnchor.Type.HasFlag(AnchorType.EndToStartOf))
        {
            return false;
        }

        if (scene.GetElement(endAnchor.Target) is not ViewConstraint sibling)
        {
            return false;
        }

        var startAnchor = startAnchorGetter(sibling);
        if (startAnchor is null || !startAnchor.Type.HasFlag(AnchorType.StartToEndOf) || startAnchor.Target != view.Id)
        {
            return false;
        }

        return true;
    }

    private ViewConstraint GetChainTail(IConstraintLayoutScene scene, Func<ViewConstraint, Anchor?> startAnchorGetter, Func<ViewConstraint, Anchor?> endAnchorGetter)
    {
        var view = this;
        while (true)
        {
            var endAnchor = endAnchorGetter(view);
            if (endAnchor is null || !endAnchor.Type.HasFlag(AnchorType.EndToStartOf))
            {
                break;
            }

            if (scene.GetElement(endAnchor.Target) is not ViewConstraint sibling)
            {
                break;
            }

            var startAnchor = startAnchorGetter(sibling);
            if (startAnchor is null || !startAnchor.Type.HasFlag(AnchorType.StartToEndOf) || startAnchor.Target != view.Id)
            {
                break;
            }

            view = sibling;
        }

        return view;
    }

    private ViewConstraint GetChainHead(IConstraintLayoutScene scene, Func<ViewConstraint, Anchor?> startAnchorGetter, Func<ViewConstraint, Anchor?> endAnchorGetter)
    {
        var view = this;
        while (true)
        {
            var startAnchor = startAnchorGetter(view);
            if (startAnchor is null || !startAnchor.Type.HasFlag(AnchorType.StartToEndOf))
            {
                break;
            }

            if (scene.GetElement(startAnchor.Target) is not ViewConstraint sibling)
            {
                break;
            }

            var endAnchor = endAnchorGetter(sibling);
            if (endAnchor is null || !endAnchor.Type.HasFlag(AnchorType.EndToStartOf) || endAnchor.Target != view.Id)
            {
                break;
            }

            view = sibling;
        }

        return view;
    }

    private Constraint? GetChainWeightedSizeConstraint(IConstraintLayoutScene scene, Func<ViewConstraint, Variable> chainWeightedSize, Func<ViewConstraint, Anchor?> startAnchorGetter, Func<ViewConstraint, Anchor?> endAnchorGetter)
    {
        var view = GetChainHead(scene, startAnchorGetter, endAnchorGetter);
        return view == this ? null : (chainWeightedSize(this) - chainWeightedSize(view)) | Eq(Required) | 0;
    }

    private AnchorConstraints? CreateBottomAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = LessOrEq(Medium);

        if (_bottomAnchor is { } bottomAnchor)
        {
            return CreateAnchorConstraint(scene, op, bottomAnchor, -1);
        }

        return null;
    }

    private AnchorConstraints? CreateTopAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = GreaterOrEq(Medium);

        if (_topAnchor is { } topAnchor)
        {
            return CreateAnchorConstraint(scene, op, topAnchor);
        }

        return null;
    }

    private AnchorConstraints? CreateRightAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = LessOrEq(Medium);

        if (_rightAnchor is { } rightAnchor)
        {
            return CreateAnchorConstraint(scene, op, rightAnchor, -1);
        }

        return null;
    }

    private AnchorConstraints? CreateLeftAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = GreaterOrEq(Medium);

        if (_leftAnchor is { } leftAnchor)
        {
            return CreateAnchorConstraint(scene, op, leftAnchor);
        }

        return null;
    }

    private AnchorConstraints? CreateAnchorConstraint(IConstraintLayoutScene scene, WeightedRelation op, Anchor anchor, double marginMultiplier = 1)
    {
        if (anchor.Tight)
        {
            op = Eq(Required);
        }

        var targetElement = scene.GetElement(anchor.Target);
        if (targetElement is null)
        {
            return null;
        }

        var anchorVariable = anchor.TargetFunc(targetElement);
        var sourceVariable = anchor.SourceFunc(this);
        Expression anchorExpression;

        var isVisible = IsVisible;
        var margin = anchor.Margin * marginMultiplier;
        var goneMargin = anchor.GoneMargin * marginMultiplier;
        if (targetElement is ViewConstraint targetViewConstraint)
        {
            var targetVisibilityMultiplier = targetViewConstraint._visibilityMultiplier;
            if (isVisible && margin != 0)
            {
                if (goneMargin != 0)
                {
                    var goneMarginTerm = goneMargin * (1 - targetVisibilityMultiplier);
                    var marginTerm = margin * targetVisibilityMultiplier;
                    anchorExpression = anchorVariable + goneMarginTerm + marginTerm;
                }
                else
                {
                    var marginTerm = margin * targetVisibilityMultiplier;
                    anchorExpression = anchorVariable + marginTerm;
                }
            }
            else if (isVisible && goneMargin != 0)
            {
                var goneMarginTerm = goneMargin * (1 - targetVisibilityMultiplier);
                anchorExpression = anchorVariable + goneMarginTerm;
            }
            else
            {
                anchorExpression = anchorVariable + 0;
            }
        }
        else
        {
            anchorExpression = anchorVariable + (isVisible ? margin : 0);
        }

        var anchorConstraint = sourceVariable | op | anchorExpression;
        return (anchorConstraint, anchorExpression);
    }

    private void NotifyHorizontalPropertyChanged(bool invalidateScene = true)
    {
        SetConstraint(ConstraintTypes.Horizontal, GetHorizontalConstraints);
        if (invalidateScene)
        {
            Scene?.InvalidateScene();
        }
    }

    private void NotifyVerticalPropertyChanged(bool invalidateScene = true)
    {
        SetConstraint(ConstraintTypes.Vertical, GetVerticalConstraints);
        if (invalidateScene)
        {
            Scene?.InvalidateScene();
        }
    }

    private void NotifyWidthPropertyChanged(bool invalidateScene = true)
    {
        SetConstraint(ConstraintTypes.Width, GetWidthConstraints);
        NotifyHorizontalPropertyChanged(invalidateScene);
    }

    private void NotifyHeightPropertyChanged(bool invalidateScene = true)
    {
        SetConstraint(ConstraintTypes.Height, GetHeightConstraints);
        NotifyVerticalPropertyChanged(invalidateScene);
    }

    private void NotifyVisibilityChanged(bool invalidateScene = true)
    {
        SetConstraint(ConstraintTypes.Visibility, _ => [_visibilityMultiplier | Eq(Required) | (IsVisible ? 1 : 0)]);
        NotifyWidthPropertyChanged(false);
        NotifyHeightPropertyChanged(false);
        if (invalidateScene)
        {
            Scene?.InvalidateScene();
        }
    }

#pragma warning disable IDE0060
    private static IEnumerable<Constraint> GetSizeConstraints(SizeDefinition size, Variable measured, Variable start, Variable end, Variable chainWeightedSize, Variable sceneStart, Variable sceneEnd, Expression otherAxisSize)
#pragma warning restore IDE0060
    {
        var multiplier = size.Multiplier;
        switch (size.Unit)
        {
            case SizeUnit.Measured:
                yield return (end - start) | Eq(Strong) | (measured * multiplier);
                break;
            case SizeUnit.Constraint:
                yield return (end - start) | Eq(Strong) | (chainWeightedSize * multiplier);
                break;
            case SizeUnit.Parent:
                yield return (end - start) | Eq(Strong) | ((sceneEnd - sceneStart) * multiplier);
                break;
            case SizeUnit.Ratio:
                yield return (end - start) | Eq(Strong) | (otherAxisSize * multiplier);
                break;
            default:
                throw new NotSupportedException();
        }
    }
}
