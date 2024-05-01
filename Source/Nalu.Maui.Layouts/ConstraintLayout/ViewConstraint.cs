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
    private readonly Variable _visibilityMultiplier = new();

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
        _visibilityMultiplier.SetName($"{id}.VisibilityMultiplier");
    }

    private IEnumerable<Constraint> GetWidthConstraints(IConstraintLayoutScene scene)
        => !IsVisible
            ? [(Right - Left) | Eq(Required) | 0]
            : GetSizeConstraints(Width, _measuredWidth, Left, Right, _leftAnchor, _rightAnchor, _chainWeightedWidth, scene.Left, scene.Right, Bottom - Top);

    private IEnumerable<Constraint> GetHeightConstraints(IConstraintLayoutScene scene)
        => !IsVisible
            ? [(Bottom - Top) | Eq(Required) | 0]
            : GetSizeConstraints(Height, _measuredHeight, Top, Bottom, _topAnchor, _bottomAnchor, _chainWeightedHeight, scene.Top, scene.Bottom, Right - Left);

    private IEnumerable<Constraint> GetHorizontalConstraints(IConstraintLayoutScene scene)
        => GetPositionConstraints(
            scene,
            v => v.CreateLeftAnchorConstraint(scene),
            v => v.CreateRightAnchorConstraint(scene),
            v => v._chainWeightedWidth,
            v => NormalizeAnchorId(v.LeftToRightOf),
            v => NormalizeAnchorId(v.RightToLeftOf),
            v => v.Left,
            v => v.Right,
            _leftAnchor,
            _rightAnchor,
            HorizontalBias);

    private IEnumerable<Constraint> GetVerticalConstraints(IConstraintLayoutScene scene)
        => GetPositionConstraints(
            scene,
            v => v.CreateTopAnchorConstraint(scene),
            v => v.CreateBottomAnchorConstraint(scene),
            v => v._chainWeightedHeight,
            v => NormalizeAnchorId(v.TopToBottomOf),
            v => NormalizeAnchorId(v.BottomToTopOf),
            v => v.Top,
            v => v.Bottom,
            _topAnchor,
            _bottomAnchor,
            VerticalBias);

    private IEnumerable<Constraint> GetPositionConstraints(
        IConstraintLayoutScene scene,
        Func<ViewConstraint, AnchorConstraints?> createStartAnchorConstraint,
        Func<ViewConstraint, AnchorConstraints?> createEndAnchorConstraint,
        Func<ViewConstraint, Variable> chainWeightedSize,
        Func<ViewConstraint, string?> startToEndOf,
        Func<ViewConstraint, string?> endToStartOf,
        Func<ViewConstraint, Variable> startGetter,
        Func<ViewConstraint, Variable> endGetter,
        Variable startAnchorVariable,
        Variable endAnchorVariable,
        double bias)
    {
        var start = startGetter(this);
        var end = endGetter(this);
        var startAnchorConstraint = createStartAnchorConstraint(this);
        var endAnchorConstraint = createEndAnchorConstraint(this);
        var isChainHead = false;
        if (startAnchorConstraint is not null)
        {
            var (startConstraint, startAnchor) = startAnchorConstraint.Value;
            yield return startConstraint;
            yield return startAnchorVariable | Eq(Required) | startAnchor;

            // Chain elements (except the head) should constrain their size to the chain weighted size variable found inside the head.
            var chainWeightedWidthConstraint = GetChainWeightedSizeConstraint(scene, chainWeightedSize, startToEndOf, endToStartOf);
            if (chainWeightedWidthConstraint is not null)
            {
                yield return chainWeightedWidthConstraint.Value;
            }
            else if (endAnchorConstraint?.Anchor is { } rightAnchor)
            {
                isChainHead = HasChainedEnd(scene, startToEndOf, endToStartOf);
                if (!isChainHead)
                {
                    yield return chainWeightedSize(this) | Eq(Required) | (rightAnchor - startAnchor);
                }
            }
        }

        var hasVariableLeftConstraint = startAnchorConstraint?.AnchorConstraint is { Strength: < Required };
        var hasVariableRightConstraint = endAnchorConstraint?.AnchorConstraint is { Strength: < Required };
        if (hasVariableLeftConstraint && hasVariableRightConstraint)
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
            var tail = GetChainTail(scene, startToEndOf, endToStartOf);
            var tailStartAnchorConstraint = createStartAnchorConstraint(tail);
            var tailEndAnchorConstraint = createEndAnchorConstraint(tail);
            var tailHasVariableStartConstraint = tailStartAnchorConstraint?.AnchorConstraint is { Strength: < Required };
            var tailHasVariableEndConstraint = tailEndAnchorConstraint?.AnchorConstraint is { Strength: < Required };
            if (tailEndAnchorConstraint is not null && tailHasVariableEndConstraint && !tailHasVariableStartConstraint)
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

        return view == this ? null : (chainWeightedSize(this) - chainWeightedSize(view)) | Eq(Required) | 0;
    }

    private AnchorConstraints? CreateBottomAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = LessOrEq(Medium);

        if (BottomToBottomOf is { } bottomToBottomOf)
        {
            return CreateAnchorConstraint(scene, op, Bottom, bottomToBottomOf, e => e.Bottom, -Margin.Bottom, -GoneMargin.Bottom);
        }

        if (BottomToTopOf is { } bottomToTopOf)
        {
            return CreateAnchorConstraint(scene, op, Bottom, bottomToTopOf, e => e.Top, -Margin.Bottom, -GoneMargin.Bottom);
        }

        return null;
    }

    private AnchorConstraints? CreateTopAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = GreaterOrEq(Medium);

        if (TopToTopOf is { } topToTopOf)
        {
            return CreateAnchorConstraint(scene, op, Top, topToTopOf, e => e.Top, Margin.Top, GoneMargin.Top);
        }

        if (TopToBottomOf is { } topToBottomOf)
        {
            return CreateAnchorConstraint(scene, op, Top, topToBottomOf, e => e.Bottom, Margin.Top, GoneMargin.Top);
        }

        return null;
    }

    private AnchorConstraints? CreateRightAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = LessOrEq(Medium);

        if (RightToRightOf is { } rightToRightOf)
        {
            return CreateAnchorConstraint(scene, op, Right, rightToRightOf, e => e.Right, -Margin.Right, -GoneMargin.Right);
        }

        if (RightToLeftOf is { } rightToLeftOf)
        {
            return CreateAnchorConstraint(scene, op, Right, rightToLeftOf, e => e.Left, -Margin.Right, -GoneMargin.Right);
        }

        return null;
    }

    private AnchorConstraints? CreateLeftAnchorConstraint(IConstraintLayoutScene scene)
    {
        var op = GreaterOrEq(Medium);

        if (LeftToLeftOf is { } leftToLeftOf)
        {
            return CreateAnchorConstraint(scene, op, Left, leftToLeftOf, e => e.Left, Margin.Left, GoneMargin.Left);
        }

        if (LeftToRightOf is { } leftToRightOf)
        {
            return CreateAnchorConstraint(scene, op, Left, leftToRightOf, e => e.Right, Margin.Left, GoneMargin.Left);
        }

        return null;
    }

    private AnchorConstraints? CreateAnchorConstraint(IConstraintLayoutScene scene, WeightedRelation op, Variable sourceVariable, string targetId, Func<ISceneElementBase, Variable> targetVariableGetter, double margin, double goneMargin)
    {
        if (targetId[^1] == '!')
        {
            op = Eq(Required);
            targetId = targetId[..^1];
        }

        var targetElement = scene.GetElement(targetId);
        if (targetElement is null)
        {
            return null;
        }

        var anchor = targetVariableGetter(targetElement);
        Expression anchorExpression;

        var isVisible = IsVisible;
        if (targetElement is ViewConstraint targetViewConstraint)
        {
            var targetVisibilityMultiplier = targetViewConstraint._visibilityMultiplier;
            if (isVisible && margin != 0)
            {
                if (goneMargin != 0)
                {
                    var goneMarginTerm = goneMargin * (1 - targetVisibilityMultiplier);
                    var marginTerm = margin * targetVisibilityMultiplier;
                    anchorExpression = anchor + goneMarginTerm + marginTerm;
                }
                else
                {
                    var marginTerm = margin * targetVisibilityMultiplier;
                    anchorExpression = anchor + marginTerm;
                }
            }
            else if (isVisible && goneMargin != 0)
            {
                var goneMarginTerm = goneMargin * (1 - targetVisibilityMultiplier);
                anchorExpression = anchor + goneMarginTerm;
            }
            else
            {
                anchorExpression = anchor + 0;
            }
        }
        else
        {
            anchorExpression = anchor + (isVisible ? margin : 0);
        }

        var anchorConstraint = sourceVariable | op | anchorExpression;
        return (anchorConstraint, anchorExpression);
    }

#pragma warning disable IDE0060
    private static IEnumerable<Constraint> GetSizeConstraints(SizeDefinition size, Variable measured, Variable start, Variable end, Variable startAnchor, Variable endAnchor, Variable chainWeightedSize, Variable sceneStart, Variable sceneEnd, Expression otherAxisSize)
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
        viewConstraint.SetConstraint(ConstraintTypes.Visibility, _ => [viewConstraint._visibilityMultiplier | Eq(Required) | (viewConstraint.IsVisible ? 1 : 0)]);
        viewConstraint.SetConstraint(ConstraintTypes.Height, viewConstraint.GetHeightConstraints);
        viewConstraint.SetConstraint(ConstraintTypes.Width, viewConstraint.GetWidthConstraints);
        viewConstraint.SetConstraint(ConstraintTypes.Horizontal, viewConstraint.GetHorizontalConstraints);
        viewConstraint.SetConstraint(ConstraintTypes.Vertical, viewConstraint.GetVerticalConstraints);

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
