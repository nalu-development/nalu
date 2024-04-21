namespace Nalu;

using Cassowary;
using static Cassowary.Strength;
using static Cassowary.WeightedRelation;
using AnchorConstraints = (Cassowary.Constraint MarginConstraint, Cassowary.Constraint? AnchorConstraint, Cassowary.Variable? Anchor);

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
    }
#pragma warning restore CS1591,SA1602

    /// <summary>
    /// Identifies the TopToTopOf bindable property.
    /// This property is used to align the top edge of the element with the top edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty TopToTopOfProperty = BindableProperty.Create(nameof(TopToTopOf), typeof(string), typeof(ViewConstraint), propertyChanged: VerticalPropertyChanged);

    /// <summary>
    /// Identifies the LeftToLeftOf bindable property.
    /// This property is used to align the left edge of the element with the left edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty LeftToLeftOfProperty = BindableProperty.Create(nameof(LeftToLeftOf), typeof(string), typeof(ViewConstraint), propertyChanged: HorizontalPropertyChanged);

    /// <summary>
    /// Identifies the TopToBottomOf bindable property.
    /// This property is used to align the top edge of the element with the bottom edge of another element.
    /// </summary>
    public static readonly BindableProperty TopToBottomOfProperty = BindableProperty.Create(nameof(TopToBottomOf), typeof(string), typeof(ViewConstraint), propertyChanged: VerticalPropertyChanged);

    /// <summary>
    /// Identifies the LeftToRightOf bindable property.
    /// This property is used to align the left edge of the element with the right edge of another element.
    /// </summary>
    public static readonly BindableProperty LeftToRightOfProperty = BindableProperty.Create(nameof(LeftToRightOf), typeof(string), typeof(ViewConstraint), propertyChanged: HorizontalPropertyChanged);

    /// <summary>
    /// Identifies the RightToRightOf bindable property.
    /// This property is used to align the right edge of the element with the right edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty RightToRightOfProperty = BindableProperty.Create(nameof(RightToRightOf), typeof(string), typeof(ViewConstraint), propertyChanged: HorizontalPropertyChanged);

    /// <summary>
    /// Identifies the RightToLeftOf bindable property.
    /// This property is used to align the right edge of the element with the left edge of another element.
    /// </summary>
    public static readonly BindableProperty RightToLeftOfProperty = BindableProperty.Create(nameof(RightToLeftOf), typeof(string), typeof(ViewConstraint), propertyChanged: HorizontalPropertyChanged);

    /// <summary>
    /// Identifies the BottomToTopOf bindable property.
    /// This property is used to align the bottom edge of the element with the top edge of another element.
    /// </summary>
    public static readonly BindableProperty BottomToTopOfProperty = BindableProperty.Create(nameof(BottomToTopOf), typeof(string), typeof(ViewConstraint), propertyChanged: VerticalPropertyChanged);

    /// <summary>
    /// Identifies the BottomToBottomOf bindable property.
    /// This property is used to align the bottom edge of the element with the bottom edge of another element or the parent.
    /// </summary>
    public static readonly BindableProperty BottomToBottomOfProperty = BindableProperty.Create(nameof(BottomToBottomOf), typeof(string), typeof(ViewConstraint), propertyChanged: VerticalPropertyChanged);

    /// <summary>
    /// Identifies the Margin bindable property.
    /// This property is used to set the margin of the element.
    /// </summary>
    public static readonly BindableProperty MarginProperty = BindableProperty.Create(nameof(Margin), typeof(Thickness), typeof(ViewConstraint), propertyChanged: MarginPropertyChanged);

    /// <summary>
    /// Identifies the GoneMargin bindable property.
    /// This property is used to set the margin of the element when it's not visible.
    /// </summary>
    public static readonly BindableProperty GoneMarginProperty = BindableProperty.Create(nameof(GoneMargin), typeof(Thickness), typeof(ViewConstraint), propertyChanged: MarginPropertyChanged);

    /// <summary>
    /// Identifies the Height bindable property.
    /// </summary>
    public static readonly BindableProperty HeightProperty = BindableProperty.Create(nameof(Height), typeof(SizeDefinition), typeof(ViewConstraint), SizeDefinition.Measured, propertyChanged: HeightPropertyChanged);

    /// <summary>
    /// Identifies the Width bindable property.
    /// </summary>
    public static readonly BindableProperty WidthProperty = BindableProperty.Create(nameof(Width), typeof(SizeDefinition), typeof(ViewConstraint), SizeDefinition.Measured, propertyChanged: WidthPropertyChanged);

    /// <summary>
    /// Identifies the HorizontalBias bindable property.
    /// </summary>
    public static readonly BindableProperty HorizontalBiasProperty = BindableProperty.Create(nameof(HorizontalBias), typeof(double), typeof(ViewConstraint), 0.5d, propertyChanged: HorizontalPropertyChanged);

    /// <summary>
    /// Identifies the VerticalBias bindable property.
    /// </summary>
    public static readonly BindableProperty VerticalBiasProperty = BindableProperty.Create(nameof(VerticalBias), typeof(double), typeof(ViewConstraint), 0.5d, propertyChanged: VerticalPropertyChanged);

    /// <summary>
    /// Identifies the IsVisible bindable property.
    /// This property is used internally to track the visibility of the target view.
    /// </summary>
    private static readonly BindableProperty _isVisibleProperty = BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(ViewConstraint), true, propertyChanged: IsVisiblePropertyChanged);

    private readonly Variable _measuredWidth = new();
    private readonly Variable _measuredHeight = new();
    private readonly Variable _chainWeightedWidth = new();
    private readonly Variable _chainWeightedHeight = new();
    private readonly Variable _leftAnchor = new();
    private readonly Variable _rightAnchor = new();
    private readonly Variable _topAnchor = new();
    private readonly Variable _bottomAnchor = new();

    /// <inheritdoc />
    public Variable ViewLeft { get; } = new();

    /// <inheritdoc />
    public Variable ViewRight { get; } = new();

    /// <inheritdoc />
    public Variable ViewTop { get; } = new();

    /// <inheritdoc />
    public Variable ViewBottom { get; } = new();

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
    /// Gets or sets the margin of the element.
    /// </summary>
    public Thickness Margin
    {
        get => (Thickness)GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin of the element when the target is not visible.
    /// </summary>
    public Thickness GoneMargin
    {
        get => (Thickness)GetValue(GoneMarginProperty);
        set => SetValue(GoneMarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the top edge with.
    /// </summary>
    public string? TopToTopOf
    {
        get => (string?)GetValue(TopToTopOfProperty);
        set => SetValue(TopToTopOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the left edge with.
    /// </summary>
    public string? LeftToLeftOf
    {
        get => (string?)GetValue(LeftToLeftOfProperty);
        set => SetValue(LeftToLeftOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the top edge with its bottom edge.
    /// </summary>
    public string? TopToBottomOf
    {
        get => (string?)GetValue(TopToBottomOfProperty);
        set => SetValue(TopToBottomOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the left edge with its right edge.
    /// </summary>
    public string? LeftToRightOf
    {
        get => (string?)GetValue(LeftToRightOfProperty);
        set => SetValue(LeftToRightOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the right edge with.
    /// </summary>
    public string? RightToRightOf
    {
        get => (string?)GetValue(RightToRightOfProperty);
        set => SetValue(RightToRightOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the right edge with its left edge.
    /// </summary>
    public string? RightToLeftOf
    {
        get => (string?)GetValue(RightToLeftOfProperty);
        set => SetValue(RightToLeftOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the bottom edge with its top edge.
    /// </summary>
    public string? BottomToTopOf
    {
        get => (string?)GetValue(BottomToTopOfProperty);
        set => SetValue(BottomToTopOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the bottom edge with.
    /// </summary>
    public string? BottomToBottomOf
    {
        get => (string?)GetValue(BottomToBottomOfProperty);
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
        IsVisiblePropertyChanged(this, false, true);
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
                constrainedWidth = ViewRight.CurrentValue - ViewLeft.CurrentValue;
            }

            if (Height.Unit != SizeUnit.Measured)
            {
                constrainedHeight = ViewBottom.CurrentValue - ViewTop.CurrentValue;
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
        SetConstraint(ConstraintTypes.Measure, scene =>
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
        _leftAnchor.SetName($"{id}.LeftAnchor");
        _rightAnchor.SetName($"{id}.RightAnchor");
        _topAnchor.SetName($"{id}.TopAnchor");
        _bottomAnchor.SetName($"{id}.BottomAnchor");
        _chainWeightedWidth.SetName($"{id}.ChainWeightedWidth");
        _chainWeightedHeight.SetName($"{id}.ChainWeightedHeight");
        ViewLeft.SetName($"{id}.ViewLeft");
        ViewRight.SetName($"{id}.ViewRight");
        ViewTop.SetName($"{id}.ViewTop");
        ViewBottom.SetName($"{id}.ViewBottom");
    }

    private IEnumerable<Constraint> GetWidthConstraints(IConstraintLayoutScene scene)
        => GetSizeConstraints(Width, _measuredWidth, Left, Right, ViewLeft, ViewRight, _chainWeightedWidth, scene.Left, scene.Right, Bottom - Top);

    private IEnumerable<Constraint> GetHeightConstraints(IConstraintLayoutScene scene)
        => GetSizeConstraints(Height, _measuredHeight, Top, Bottom, ViewTop, ViewBottom, _chainWeightedHeight, scene.Top, scene.Bottom, Right - Left);

    private IEnumerable<Constraint> GetHorizontalConstraints(IConstraintLayoutScene scene)
    {
        var (leftMarginConstraint, leftConstraint, leftAnchor) = CreateLeftAnchorConstraint(scene);
        var (rightMarginConstraint, rightConstraint, rightAnchor) = CreateRightAnchorConstraint(scene);

        var isChainHead = false;
        yield return leftMarginConstraint;
        if (leftConstraint.HasValue)
        {
            yield return leftConstraint.Value;
            yield return _leftAnchor | Eq(Required) | leftAnchor!;

            // Chain elements (except the head) should constrain their size to the chain weighted size variable found inside the head.
            var chainWeightedWidthConstraint = GetChainWeightedSizeConstraint(scene, v => v._chainWeightedWidth, v => NormalizeAnchorId(v.LeftToRightOf), v => NormalizeAnchorId(v.RightToLeftOf));
            if (chainWeightedWidthConstraint is not null)
            {
                yield return chainWeightedWidthConstraint.Value;
            }
            else if (rightAnchor is not null)
            {
                isChainHead = HasChainedEnd(scene, v => NormalizeAnchorId(v.LeftToRightOf), v => NormalizeAnchorId(v.RightToLeftOf));
                if (!isChainHead)
                {
                    yield return _chainWeightedWidth | Eq(Required) | (rightAnchor - leftAnchor!);
                }
            }
        }

        var hasVariableLeftConstraint = leftConstraint is { Strength: < Required };
        var hasVariableRightConstraint = rightConstraint is { Strength: < Required };
        if (hasVariableLeftConstraint && hasVariableRightConstraint)
        {
            var bias = HorizontalBias;
            yield return bias switch
            {
                1d => Right | Eq(Required) | rightAnchor!,
                0d => Left | Eq(Required) | leftAnchor!,

                // left = leftAnchor + (rightAnchor - leftAnchor - width) * bias
                // left = leftAnchor + (rightAnchor - leftAnchor - right + left) * bias
                // 0 = leftAnchor + (rightAnchor - leftAnchor - right + left) * bias - left
                // 0 = leftAnchor + bias * rightAnchor - bias * leftAnchor - bias * right + bias * left - left
                // 0 = leftAnchor - bias * leftAnchor + bias * rightAnchor - bias * right + bias * left - left
                // 0 = leftAnchor * (1 - bias) + bias * rightAnchor - bias * right + left * (bias - 1)
                // bias * right = leftAnchor * (1 - bias) + bias * rightAnchor + left * (bias - 1)
                _ => (Right * bias) | Eq(Required) | ((leftAnchor! * (1 - bias)) + (bias * rightAnchor!) + (Left * (bias - 1))),
            };
        }
        else if (isChainHead && leftAnchor is not null && hasVariableLeftConstraint && !hasVariableRightConstraint)
        {
            var tail = GetChainTail(scene, v => NormalizeAnchorId(v.LeftToRightOf), v => NormalizeAnchorId(v.RightToLeftOf));
            var (_, _, tailRightAnchor) = tail.CreateRightAnchorConstraint(scene);
            if (tailRightAnchor is not null)
            {
                var bias = HorizontalBias;
                yield return (tail.Right * bias) | Eq(Required) | ((leftAnchor * (1 - bias)) + (bias * tailRightAnchor) + (Left * (bias - 1)));
            }
        }

        yield return rightMarginConstraint;
        if (rightConstraint.HasValue)
        {
            yield return rightConstraint.Value;
            yield return _rightAnchor | Eq(Required) | rightAnchor!;
        }
    }

    private IEnumerable<Constraint> GetVerticalConstraints(IConstraintLayoutScene scene)
    {
        var (topMarginConstraint, topConstraint, topAnchor) = CreateTopAnchorConstraint(scene);
        var (bottomMarginConstraint, bottomConstraint, bottomAnchor) = CreateBottomAnchorConstraint(scene);

        var isChainHead = false;
        yield return topMarginConstraint;
        if (topConstraint.HasValue)
        {
            yield return topConstraint.Value;
            yield return _topAnchor | Eq(Required) | topAnchor!;

            // Chain elements (except the head) should constrain their size to the chain weighted size variable found inside the head.
            var chainWeightedHeightConstraint = GetChainWeightedSizeConstraint(scene, v => v._chainWeightedHeight, v => NormalizeAnchorId(v.TopToBottomOf), v => NormalizeAnchorId(v.BottomToTopOf));
            if (chainWeightedHeightConstraint is not null)
            {
                yield return chainWeightedHeightConstraint.Value;
            }
            else if (bottomAnchor is not null)
            {
                isChainHead = HasChainedEnd(scene, v => NormalizeAnchorId(v.TopToBottomOf), v => NormalizeAnchorId(v.BottomToTopOf));
                if (!isChainHead)
                {
                    yield return _chainWeightedHeight | Eq(Required) | (bottomAnchor - topAnchor!);
                }
            }
        }

        var hasVariableTopConstraint = topConstraint is { Strength: < Required };
        var hasVariableBottomConstraint = bottomConstraint is { Strength: < Required };
        if (hasVariableTopConstraint && hasVariableBottomConstraint)
        {
            var bias = VerticalBias;
            yield return bias switch
            {
                1d => Bottom | Eq(Required) | bottomAnchor!,
                0d => Top | Eq(Required) | topAnchor!,

                // top = topAnchor + (bottomAnchor - topAnchor - height) * bias
                // top = topAnchor + (bottomAnchor - topAnchor - bottom + top) * bias
                // 0 = topAnchor + (bottomAnchor - topAnchor - bottom + top) * bias - top
                // 0 = topAnchor + bias * bottomAnchor - bias * topAnchor - bias * bottom + bias * top - top
                // 0 = topAnchor - bias * topAnchor + bias * bottomAnchor - bias * bottom + bias * top - top
                // 0 = topAnchor * (1 - bias) + bias * bottomAnchor - bias * bottom + top * (bias - 1)
                // bias * bottom = topAnchor * (1 - bias) + bias * bottomAnchor + top * (bias - 1)
                _ => (Bottom * bias) | Eq(Required) | ((topAnchor! * (1 - bias)) + (bias * bottomAnchor!) + (Top * (bias - 1))),
            };
        }
        else if (isChainHead && topAnchor is not null && hasVariableTopConstraint && !hasVariableBottomConstraint)
        {
            var tail = GetChainTail(scene, v => NormalizeAnchorId(v.TopToBottomOf), v => NormalizeAnchorId(v.BottomToTopOf));
            var (_, _, tailBottomAnchor) = tail.CreateBottomAnchorConstraint(scene);
            if (tailBottomAnchor is not null)
            {
                var bias = VerticalBias;
                yield return (tail.Bottom * bias) | Eq(Required) | ((topAnchor * (1 - bias)) + (bias * tailBottomAnchor) + (Top * (bias - 1)));
            }
        }

        yield return bottomMarginConstraint;
        if (bottomConstraint.HasValue)
        {
            yield return bottomConstraint.Value;
            yield return _bottomAnchor | Eq(Required) | bottomAnchor!;
        }
    }

    private bool HasChainedEnd(IConstraintLayoutScene scene, Func<ViewConstraint, string?> startToEndOf, Func<ViewConstraint, string?> endToStartOf)
    {
        var endToStartOfId = endToStartOf(this);
        if (endToStartOfId is null)
        {
            return false;
        }

        var sibling = scene.GetElement(endToStartOfId) as ViewConstraint;
        return sibling is not null && startToEndOf(sibling) == Id;
    }

    private ViewConstraint GetChainTail(IConstraintLayoutScene scene, Func<ViewConstraint, string?> startToEndOf, Func<ViewConstraint, string?> endToStartOf)
    {
        var isChained = true;
        var view = this;
        while (isChained)
        {
            var endToStartOfId = endToStartOf(view);
            if (endToStartOfId is null)
            {
                break;
            }

            if (scene.GetElement(endToStartOfId) is not ViewConstraint sibling)
            {
                break;
            }

            var siblingStartToEndOf = startToEndOf(sibling);
            isChained = siblingStartToEndOf == view.Id;
            if (!isChained)
            {
                break;
            }

            view = sibling;
        }

        return view;
    }

    private Constraint? GetChainWeightedSizeConstraint(IConstraintLayoutScene scene, Func<ViewConstraint, Variable> chainWeightedSize, Func<ViewConstraint, string?> startToEndOf, Func<ViewConstraint, string?> endToStartOf)
    {
        var isChained = true;
        var view = this;
        while (isChained)
        {
            var startToEndOfId = startToEndOf(view);
            if (startToEndOfId is null)
            {
                break;
            }

            var sibling = scene.GetElement(startToEndOfId) as ViewConstraint;
            isChained = sibling is not null && endToStartOf(sibling) == view.Id;
            if (!isChained)
            {
                break;
            }

            view = sibling!;
        }

        return view == this ? null : (chainWeightedSize(this) - chainWeightedSize(view)) | Eq(Medium) | 0;
    }

    private AnchorConstraints CreateBottomAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = LessOrEq(Medium);

        if (BottomToBottomOf is { } bottomToBottomOf)
        {
            return CreateAnchorConstraint(scene, op, Bottom, ViewBottom, bottomToBottomOf, e => e.Bottom, -Margin.Bottom, -GoneMargin.Bottom);
        }

        if (BottomToTopOf is { } bottomToTopOf)
        {
            return CreateAnchorConstraint(scene, op, Bottom, ViewBottom, bottomToTopOf, e => e.Top, -Margin.Bottom, -GoneMargin.Bottom);
        }

        var noAnchorMarginConstraint = ViewBottom | Eq(Required) | (Bottom - Margin.Bottom);
        return (noAnchorMarginConstraint, null, null);
    }

    private AnchorConstraints CreateTopAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = GreaterOrEq(Medium);

        if (TopToTopOf is { } topToTopOf)
        {
            return CreateAnchorConstraint(scene, op, Top, ViewTop, topToTopOf, e => e.Top, Margin.Top, GoneMargin.Top);
        }

        if (TopToBottomOf is { } topToBottomOf)
        {
            return CreateAnchorConstraint(scene, op, Top, ViewTop, topToBottomOf, e => e.Bottom, Margin.Top, GoneMargin.Top);
        }

        var noAnchorMarginConstraint = ViewTop | Eq(Required) | (Top + Margin.Top);
        return (noAnchorMarginConstraint, null, null);
    }

    private AnchorConstraints CreateRightAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = LessOrEq(Medium);

        if (RightToRightOf is { } rightToRightOf)
        {
            return CreateAnchorConstraint(scene, op, Right, ViewRight, rightToRightOf, e => e.Right, -Margin.Right, -GoneMargin.Right);
        }

        if (RightToLeftOf is { } rightToLeftOf)
        {
            return CreateAnchorConstraint(scene, op, Right, ViewRight, rightToLeftOf, e => e.Left, -Margin.Right, -GoneMargin.Right);
        }

        var noAnchorMarginConstraint = ViewRight | Eq(Required) | (Right - Margin.Right);
        return (noAnchorMarginConstraint, null, null);
    }

    private AnchorConstraints CreateLeftAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = GreaterOrEq(Medium);

        if (LeftToLeftOf is { } leftToLeftOf)
        {
            return CreateAnchorConstraint(scene, op, Left, ViewLeft, leftToLeftOf, e => e.Left, Margin.Left, GoneMargin.Left);
        }

        if (LeftToRightOf is { } leftToRightOf)
        {
            return CreateAnchorConstraint(scene, op, Left, ViewLeft, leftToRightOf, e => e.Right, Margin.Left, GoneMargin.Left);
        }

        var noAnchorMarginConstraint = ViewLeft | Eq(Required) | (Left + Margin.Left);
        return (noAnchorMarginConstraint, null, null);
    }

    private static AnchorConstraints CreateAnchorConstraint(IConstraintLayoutScene scene, WeightedRelation op, Variable sourceVariable, Variable sourceViewVariable, string targetId, Func<ISceneElementBase, Variable> targetVariableGetter, double margin, double goneMargin)
    {
        if (targetId[^1] == '!')
        {
            op = Eq(Required);
            targetId = targetId[..^1];
        }

        var targetElement = scene.GetElement(targetId);
        if (targetElement is null)
        {
            var noAnchorMarginConstraint = sourceViewVariable | Eq(Required) | (sourceVariable + margin);
            return (noAnchorMarginConstraint, null, null);
        }

        var gone = scene.GetView(targetId)?.Visibility == Visibility.Collapsed;
        var anchor = targetVariableGetter(targetElement);
        var anchorConstraint = sourceVariable | op | anchor;
        var marginConstraint = sourceViewVariable | Eq(Required) | (sourceVariable + (gone ? goneMargin : margin));
        return (marginConstraint, anchorConstraint, anchor);
    }

#pragma warning disable IDE0060
    private static IEnumerable<Constraint> GetSizeConstraints(SizeDefinition size, Variable measured, Variable start, Variable end, Variable viewStart, Variable viewEnd, Variable chainWeightedSize, Variable sceneStart, Variable sceneEnd, Expression otherAxisSize)
#pragma warning restore IDE0060
    {
        var multiplier = size.Multiplier;
        switch (size.Unit)
        {
            case SizeUnit.Measured:
                yield return (viewEnd - viewStart) | Eq(Strong) | (measured * multiplier);
                break;
            case SizeUnit.Constraint:
                yield return (end - start) | LessOrEq(Strong) | (chainWeightedSize * multiplier);
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

    private static void HorizontalPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewConstraint = (ViewConstraint)bindable;
        viewConstraint.SetConstraint(ConstraintTypes.Horizontal, viewConstraint.GetHorizontalConstraints);
        viewConstraint.Scene?.InvalidateScene();
    }

    private static void WidthPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewConstraint = (ViewConstraint)bindable;
        viewConstraint.SetConstraint(ConstraintTypes.Width, viewConstraint.GetWidthConstraints);
        viewConstraint.Scene?.InvalidateScene();
    }

    private static void VerticalPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewConstraint = (ViewConstraint)bindable;
        viewConstraint.SetConstraint(ConstraintTypes.Vertical, viewConstraint.GetVerticalConstraints);
        viewConstraint.Scene?.InvalidateScene();
    }

    private static void HeightPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewConstraint = (ViewConstraint)bindable;
        viewConstraint.SetConstraint(ConstraintTypes.Height, viewConstraint.GetHeightConstraints);
        viewConstraint.Scene?.InvalidateScene();
    }

    private static void IsVisiblePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewConstraint = (ViewConstraint)bindable;
        if (newValue is true)
        {
            viewConstraint.SetConstraint(ConstraintTypes.Height, viewConstraint.GetHeightConstraints);
            viewConstraint.SetConstraint(ConstraintTypes.Width, viewConstraint.GetWidthConstraints);
        }
        else
        {
            viewConstraint.SetConstraint(ConstraintTypes.Height, _ => [viewConstraint.Right | Eq(Required) | viewConstraint.Left]);
            viewConstraint.SetConstraint(ConstraintTypes.Width, _ => [viewConstraint.Top | Eq(Required) | viewConstraint.Bottom]);
        }

        viewConstraint.Scene?.InvalidateScene();
    }

    private static void MarginPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var viewConstraint = (ViewConstraint)bindable;
        var oldThickness = (Thickness)oldValue;
        var thickness = (Thickness)newValue;

        if (oldThickness.Left != thickness.Left || oldThickness.Right != thickness.Right)
        {
            viewConstraint.SetConstraint(ConstraintTypes.Horizontal, viewConstraint.GetHorizontalConstraints);
        }

        if (oldThickness.Top != thickness.Top || oldThickness.Bottom != thickness.Bottom)
        {
            viewConstraint.SetConstraint(ConstraintTypes.Vertical, viewConstraint.GetVerticalConstraints);
        }

        viewConstraint.Scene?.InvalidateScene();
    }

    private static string? NormalizeAnchorId(string? anchorId)
    {
        if (anchorId?.Length > 0 && anchorId[^1] == '!')
        {
            return anchorId[..^1];
        }

        return anchorId;
    }
}
