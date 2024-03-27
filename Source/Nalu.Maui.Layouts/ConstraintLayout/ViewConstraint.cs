namespace Nalu;

using Cassowary;
using static Cassowary.Strength;
using static Cassowary.WeightedRelation;

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
    public static readonly BindableProperty HeightProperty = BindableProperty.Create(nameof(Height), typeof(SizeDefinition), typeof(ViewConstraint), SizeDefinition.Auto, propertyChanged: HeightPropertyChanged);

    /// <summary>
    /// Identifies the Width bindable property.
    /// </summary>
    public static readonly BindableProperty WidthProperty = BindableProperty.Create(nameof(Width), typeof(SizeDefinition), typeof(ViewConstraint), SizeDefinition.Auto, propertyChanged: WidthPropertyChanged);

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
    private readonly Variable _leftAnchor = new();
    private readonly Variable _rightAnchor = new();
    private readonly Variable _topAnchor = new();
    private readonly Variable _bottomAnchor = new();

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
    public string TopToTopOf
    {
        get => (string)GetValue(TopToTopOfProperty);
        set => SetValue(TopToTopOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the left edge with.
    /// </summary>
    public string LeftToLeftOf
    {
        get => (string)GetValue(LeftToLeftOfProperty);
        set => SetValue(LeftToLeftOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the top edge with its bottom edge.
    /// </summary>
    public string TopToBottomOf
    {
        get => (string)GetValue(TopToBottomOfProperty);
        set => SetValue(TopToBottomOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the left edge with its right edge.
    /// </summary>
    public string LeftToRightOf
    {
        get => (string)GetValue(LeftToRightOfProperty);
        set => SetValue(LeftToRightOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the right edge with.
    /// </summary>
    public string RightToRightOf
    {
        get => (string)GetValue(RightToRightOfProperty);
        set => SetValue(RightToRightOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the right edge with its left edge.
    /// </summary>
    public string RightToLeftOf
    {
        get => (string)GetValue(RightToLeftOfProperty);
        set => SetValue(RightToLeftOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the bottom edge with its top edge.
    /// </summary>
    public string BottomToTopOf
    {
        get => (string)GetValue(BottomToTopOfProperty);
        set => SetValue(BottomToTopOfProperty, value);
    }

    /// <summary>
    /// Gets or sets the ID of the element to align the bottom edge with.
    /// </summary>
    public string BottomToBottomOf
    {
        get => (string)GetValue(BottomToBottomOfProperty);
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

        IsVisible = view.Visibility == Visibility.Collapsed;
        SetConstraint(ConstraintTypes.Measure, scene =>
        {
            var sceneWidth = Width.Match == SizeUnit.MeasuredUnconstrained || double.IsPositiveInfinity(scene.Right.Value) ? double.PositiveInfinity : scene.Right.Value - scene.Left.Value;
            var sceneHeight = Height.Match == SizeUnit.MeasuredUnconstrained || double.IsPositiveInfinity(scene.Bottom.Value) ? double.PositiveInfinity : scene.Bottom.Value - scene.Top.Value;
            var measured = view.Measure(sceneWidth, sceneHeight);

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
    }

    private IEnumerable<Constraint> GetWidthConstraints(IConstraintLayoutScene scene)
    {
        yield return GetSizeConstraint(Width, _measuredWidth, Left, Right, _leftAnchor, _rightAnchor, scene.Left, scene.Right, Bottom - Top);
    }

    private IEnumerable<Constraint> GetHeightConstraints(IConstraintLayoutScene scene)
    {
        yield return GetSizeConstraint(Height, _measuredHeight, Top, Bottom, _topAnchor, _bottomAnchor, scene.Top, scene.Bottom, Right - Left);
    }

    private IEnumerable<Constraint> GetHorizontalConstraints(IConstraintLayoutScene scene)
    {
        var (leftConstraint, leftAnchor) = CreateLeftAnchorConstraint(scene);
        var (rightConstraint, rightAnchor) = CreateRightAnchorConstraint(scene);

        if (leftConstraint is { Strength: < Required } && rightConstraint is { Strength: < Required })
        {
            var bias = HorizontalBias;
            yield return bias switch
            {
                1d => Right | Eq(Required) | rightAnchor!.Value,
                0d => Left | Eq(Required) | leftAnchor!.Value,
                0.5d => ((Left + Right) * 0.5d) | Eq(Required) | ((leftAnchor!.Value + rightAnchor!.Value) * 0.5d),
                _ => Left | Eq(Strong) | (leftAnchor!.Value + ((rightAnchor!.Value - leftAnchor.Value - Right + Left) * bias)),
            };
        }

        if (leftConstraint.HasValue)
        {
            yield return leftConstraint.Value;
            yield return _leftAnchor | Eq(Required) | leftAnchor!.Value;
        }

        if (rightConstraint.HasValue)
        {
            yield return rightConstraint.Value;
            yield return _rightAnchor | Eq(Required) | rightAnchor!.Value;
        }
    }

    private IEnumerable<Constraint> GetVerticalConstraints(IConstraintLayoutScene scene)
    {
        var (topConstraint, topAnchor) = CreateTopAnchorConstraint(scene);
        var (bottomConstraint, bottomAnchor) = CreateBottomAnchorConstraint(scene);

        if (topConstraint is { Strength: < Required } && bottomConstraint is { Strength: < Required })
        {
            var bias = VerticalBias;
            yield return bias switch
            {
                1d => Bottom | Eq(Required) | bottomAnchor!.Value,
                0d => Top | Eq(Required) | topAnchor!.Value,
                0.5d => ((Top + Bottom) * bias) | Eq(Required) | ((topAnchor!.Value + bottomAnchor!.Value) * bias),
                _ => Top | Eq(Strong) | (topAnchor!.Value + ((bottomAnchor!.Value - topAnchor.Value - Bottom + Top) * bias)),
            };
        }

        if (topConstraint.HasValue)
        {
            yield return topConstraint.Value;
            yield return _topAnchor | Eq(Required) | topAnchor!.Value;
        }

        if (bottomConstraint.HasValue)
        {
            yield return bottomConstraint.Value;
            yield return _bottomAnchor | Eq(Required) | bottomAnchor!.Value;
        }
    }

    private (Constraint? Constraint, Expression? Anchor) CreateBottomAnchorConstraint(IConstraintLayoutScene scene)
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

        return (null, null);
    }

    private (Constraint? Constraint, Expression? Anchor) CreateTopAnchorConstraint(IConstraintLayoutScene scene)
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

        return (null, null);
    }

    private (Constraint? Constraint, Expression? Anchor) CreateRightAnchorConstraint(IConstraintLayoutScene scene)
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

        return (null, null);
    }

    private (Constraint? Constraint, Expression? Anchor) CreateLeftAnchorConstraint(IConstraintLayoutScene scene)
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

        return (null, null);
    }

    private static (Constraint? Constraint, Expression? Anchor) CreateAnchorConstraint(IConstraintLayoutScene scene, WeightedRelation op, Variable sourceVariable, string targetId, Func<ISceneElementBase, Variable> targetVariableGetter, double margin, double goneMargin)
    {
        if (targetId[^1] == '!')
        {
            op = Eq(Required);
            targetId = targetId[..^1];
        }

        var targetElement = scene.GetElement(targetId);
        if (targetElement is not null)
        {
            var gone = scene.GetView(targetId)?.Visibility == Visibility.Collapsed;
            var anchor = targetVariableGetter(targetElement) + (gone ? goneMargin : margin);
            var constraint = sourceVariable | op | anchor;
            return (constraint, anchor);
        }

        return (null, null);
    }

    private static Constraint GetSizeConstraint(SizeDefinition size, Variable measured, Variable start, Variable end, Variable startAnchor, Variable endAnchor, Variable sceneStart, Variable sceneEnd, Expression otherAxisSize)
    {
        var multiplier = size.Multiplier;
        return size.Match switch
        {
            SizeUnit.Measured => end | Eq(Strong) | (start + (measured * multiplier)),
            SizeUnit.MeasuredUnconstrained => end | Eq(Strong) | (start + (measured * multiplier)),
            SizeUnit.Constraint => (end + (startAnchor * multiplier)) | Eq(Strong) | (start + (endAnchor * multiplier)),
            SizeUnit.Parent => (end + (sceneStart * multiplier)) | Eq(Strong) | (start + (sceneEnd * multiplier)),
            SizeUnit.Ratio => (end - start) | Eq(Strong) | (otherAxisSize * multiplier),
            _ => throw new NotSupportedException(),
        };
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
}
