using System.Diagnostics.CodeAnalysis;
using Nalu.Cassowary;
using Nalu.Internals;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;
using PoleConnection = (Nalu.Cassowary.Expression Pole, Nalu.MagnetLayout.Traction Traction, Nalu.Cassowary.WeightedRelation Relation, Nalu.Cassowary.Expression ToPole, Nalu.MagnetLayout.MagnetView? ChainedView);

namespace Nalu.MagnetLayout;

/// <summary>
/// Represents a view in a <see cref="Magnet" /> layout.
/// </summary>
public class MagnetView : MagnetElementBase<MagnetView.ConstraintTypes>, IMagnetView, IHorizontalPoles, IVerticalPoles, IHorizontalChainPoles, IVerticalChainPoles
{
#pragma warning disable CS1591
    public enum ConstraintTypes
    {
        MeasuredWidth,
        MeasuredHeight,
        Width,
        Height,
        HorizontalPosition,
        VerticalPosition
    }
#pragma warning restore CS1591

    /// <summary>
    /// Bindable property for the <see cref="Width" /> property.
    /// </summary>
    public static readonly BindableProperty WidthProperty = GenericBindableProperty<MagnetView>.Create(
        nameof(Width),
        SizeValue.Default,
        propertyChanged: b => b.OnWidthChanged
    );

    /// <summary>
    /// Gets or sets the width sizing strategy for this view.
    /// </summary>
    public SizeValue Width
    {
        get => (SizeValue) GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="Height" /> property.
    /// </summary>
    public static readonly BindableProperty HeightProperty = GenericBindableProperty<MagnetView>.Create(
        nameof(Height),
        SizeValue.Default,
        propertyChanged: b => b.OnHeightChanged
    );

    /// <summary>
    /// Gets or sets the height sizing strategy for this view.
    /// </summary>
    public SizeValue Height
    {
        get => (SizeValue) GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="HorizontalBias" /> property.
    /// </summary>
    public static readonly BindableProperty HorizontalBiasProperty = GenericBindableProperty<MagnetView>.Create(
        nameof(HorizontalBias),
        0.5,
        propertyChanged: b => b.OnHorizontalBiasChanged
    );

    /// <summary>
    /// Gets or sets the horizontal positioning bias for this view.
    /// </summary>
    /// <remarks>
    /// This is only effective when the <see cref="Width" /> is set to <see cref="SizeUnit.Constraint" />.
    /// </remarks>
    public double HorizontalBias
    {
        get => (double) GetValue(HorizontalBiasProperty);
        set => SetValue(HorizontalBiasProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="VerticalBias" /> property.
    /// </summary>
    public static readonly BindableProperty VerticalBiasProperty = GenericBindableProperty<MagnetView>.Create(
        nameof(VerticalBias),
        0.5,
        propertyChanged: b => b.OnVerticalBiasChanged
    );

    /// <summary>
    /// Gets or sets the horizontal positioning bias for this view.
    /// </summary>
    /// <remarks>
    /// This is only effective when the <see cref="Width" /> is set to <see cref="SizeUnit.Constraint" />.
    /// </remarks>
    public double VerticalBias
    {
        get => (double) GetValue(VerticalBiasProperty);
        set => SetValue(VerticalBiasProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="LeftTo" /> property.
    /// </summary>
    public static readonly BindableProperty LeftToProperty = GenericBindableProperty<MagnetView>.Create<HorizontalPullTarget?>(
        nameof(LeftTo),
        propertyChanged: b => b.OnLeftToChanged
    );

    /// <summary>
    /// Gets or sets the <see cref="HorizontalPullTarget" /> to which the left side of this view will be attracted to.
    /// </summary>
    public HorizontalPullTarget? LeftTo
    {
        get => (HorizontalPullTarget?) GetValue(LeftToProperty);
        set => SetValue(LeftToProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="RightTo" /> property.
    /// </summary>
    public static readonly BindableProperty RightToProperty = GenericBindableProperty<MagnetView>.Create<HorizontalPullTarget?>(
        nameof(RightTo),
        propertyChanged: b => b.OnRightToChanged
    );

    /// <summary>
    /// Gets or sets the <see cref="HorizontalPullTarget" /> to which the right side of this view will be attracted to.
    /// </summary>
    public HorizontalPullTarget? RightTo
    {
        get => (HorizontalPullTarget?) GetValue(RightToProperty);
        set => SetValue(RightToProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="TopTo" /> property.
    /// </summary>
    public static readonly BindableProperty TopToProperty = GenericBindableProperty<MagnetView>.Create<VerticalPullTarget?>(
        nameof(TopTo),
        propertyChanged: b => b.OnTopToChanged
    );

    /// <summary>
    /// Gets or sets the <see cref="VerticalPullTarget" /> to which the top side of this view will be attracted to.
    /// </summary>
    public VerticalPullTarget? TopTo
    {
        get => (VerticalPullTarget?) GetValue(TopToProperty);
        set => SetValue(TopToProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="BottomTo" /> property.
    /// </summary>
    public static readonly BindableProperty BottomToProperty = GenericBindableProperty<MagnetView>.Create<VerticalPullTarget?>(
        nameof(BottomTo),
        propertyChanged: b => b.OnBottomToChanged
    );

    /// <summary>
    /// Gets or sets the <see cref="VerticalPullTarget" /> to which the bottom side of this view will be attracted to.
    /// </summary>
    public VerticalPullTarget? BottomTo
    {
        get => (VerticalPullTarget?) GetValue(BottomToProperty);
        set => SetValue(BottomToProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="Margin" /> property.
    /// </summary>
    public static readonly BindableProperty MarginProperty = GenericBindableProperty<MagnetView>.Create<Thickness>(
        nameof(Margin),
        propertyChanged: b => b.OnMarginChanged
    );

    /// <summary>
    /// Gets or sets the view margins
    /// </summary>
    public Thickness Margin
    {
        get => (Thickness) GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    /// <summary>
    /// Bindable property for the <see cref="CollapsedMargin" /> property.
    /// </summary>
    public static readonly BindableProperty CollapsedMarginProperty = GenericBindableProperty<MagnetView>.Create<Thickness>(
        nameof(CollapsedMargin),
        propertyChanged: b => b.OnMarginChanged
    );

    /// <summary>
    /// Gets or sets the view margins when the pull target has <see cref="Visibility"/> set to <see cref="Visibility.Collapsed"/>.
    /// </summary>
    public Thickness CollapsedMargin
    {
        get => (Thickness) GetValue(CollapsedMarginProperty);
        set => SetValue(CollapsedMarginProperty, value);
    }

    /// <inheritdoc />
    public Visibility Visibility => View?.Visibility ?? Visibility.Collapsed;

    /// <inheritdoc />
    Variable IHorizontalPoles.Left => _left;
    private readonly Variable _left = new();

    /// <inheritdoc />
    Variable IHorizontalPoles.Right => _right;
    private readonly Variable _right = new();

    /// <inheritdoc />
    Variable IVerticalPoles.Top => _top;
    private readonly Variable _top = new();

    /// <inheritdoc />
    Variable IVerticalPoles.Bottom => _bottom;
    private readonly Variable _bottom = new();

    /// <inheritdoc />
    Variable IHorizontalChainPoles.ChainLeft => _chainLeft;
    private readonly Variable _chainLeft = new();

    /// <inheritdoc />
    Variable IHorizontalChainPoles.ChainRight => _chainRight;
    private readonly Variable _chainRight = new();

    /// <inheritdoc />
    Variable IVerticalChainPoles.ChainTop => _chainTop;
    private readonly Variable _chainTop = new();

    /// <inheritdoc />
    Variable IVerticalChainPoles.ChainBottom => _chainBottom;
    private readonly Variable _chainBottom = new();

    /// <summary>
    /// Gets the top position of the view.
    /// </summary>
    public double Top => _top.CurrentValue;

    /// <summary>
    /// Gets the bottom position of the view.
    /// </summary>
    public double Bottom => _bottom.CurrentValue;
    
    /// <summary>
    /// Gets the left position of the view.
    /// </summary>
    public double Left => _left.CurrentValue;

    /// <summary>
    /// Gets the right position of the view.
    /// </summary>
    public double Right => _right.CurrentValue;

    /// <summary>
    /// Internally used to store the real view associated with this magnet view.
    /// </summary>
    internal IView? View
    {
        get => _viewRef?.TryGetTarget(out var view) is true ? view : null;
        set => _viewRef = value == null ? null : new WeakReference<IView>(value);
    }

    private readonly Variable _measuredWidth = new();
    private readonly Variable _measuredHeight = new();
    private readonly Variable _constraintsWidth = new();
    private readonly Variable _constraintsHeight = new();
    private readonly Variable _desiredWidth = new();
    private readonly Variable _desiredHeight = new();
    private readonly Variable _horizontalTailSpace = new();
    private readonly Variable _verticalTailSpace = new();
    private double _marginTop = double.NaN;
    private double _marginBottom = double.NaN;
    private double _marginLeft = double.NaN;
    private double _marginRight = double.NaN;
    private WeakReference<IView>? _viewRef;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagnetView" /> class.
    /// </summary>
    public MagnetView()
    {
        UpdateConstraints(ConstraintTypes.Width, GetWidthConstraints);
        UpdateConstraints(ConstraintTypes.Height, GetHeightConstraints);
    }

    /// <inheritdoc />
    protected override void SetVariableNames(string id)
    {
        _top.SetName($"{id}.Top");
        _bottom.SetName($"{id}.Bottom");
        _left.SetName($"{id}.Left");
        _right.SetName($"{id}.Right");
        _chainTop.SetName($"{id}.ChainTop");
        _chainBottom.SetName($"{id}.ChainBottom");
        _chainLeft.SetName($"{id}.ChainLeft");
        _chainRight.SetName($"{id}.ChainRight");
        _measuredWidth.SetName($"{id}.MeasuredWidth");
        _measuredHeight.SetName($"{id}.MeasuredHeight");
        _constraintsWidth.SetName($"{id}.ConstraintsWidth");
        _constraintsHeight.SetName($"{id}.ConstraintsHeight");
        _desiredWidth.SetName($"{id}.DesiredWidth");
        _desiredHeight.SetName($"{id}.DesiredHeight");
        _horizontalTailSpace.SetName($"{id}.HorizontalTailSpace");
        _verticalTailSpace.SetName($"{id}.VerticalTailSpace");
    }

    /// <inheritdoc />
    public Thickness GetEffectiveMargin() => new Thickness(_marginLeft, _marginTop, _marginRight, _marginBottom);

    private void OnMarginChanged(Thickness oldValue, Thickness newValue) => InvalidateStage();

    /// <inheritdoc />
    protected override void ApplyConstraints(IMagnetStage stage)
    {
        // TODO: Skip this on ArrangePhase
        UpdatePolesConstraintsIfNeeded(stage);

        if (View is { } view)
        {
            // We have to measure the view when either the width or height is set to auto.
            if (Height.Unit is SizeUnit.Measured || Width.Unit is SizeUnit.Measured)
            {
                var widthConstraint = stage.Right.CurrentValue - stage.Left.CurrentValue;
                var heightConstraint = stage.Bottom.CurrentValue - stage.Top.CurrentValue;
                var size = view.Measure(widthConstraint, heightConstraint);

                UpdateMeasureConstraints(size);
            }
        }

        base.ApplyConstraints(stage);
    }

    /// <inheritdoc />
    protected override void FinalizeConstraints(IMagnetStage stage)
    {
        if (View is not { } view)
        {
            return;
        }

        // If the allocated space is less than the desired size, we need to re-measure the view
        var needsMeasure = false;
        var widthConstraint = stage.Right.CurrentValue - stage.Left.CurrentValue;
        var heightConstraint = stage.Bottom.CurrentValue - stage.Top.CurrentValue;

        if (Height.Unit is SizeUnit.Measured && view.DesiredSize.Width > Right - Left)
        {
            widthConstraint = Right - Left;
            needsMeasure = true;
        }

        if (Width.Unit is SizeUnit.Measured && view.DesiredSize.Height > Bottom - Top)
        {
            heightConstraint = Bottom - Top;
            needsMeasure = true;
        }

        if (needsMeasure)
        {
            var size = view.Measure(widthConstraint, heightConstraint);
            UpdateMeasureConstraints(size);
        }
    }

    private void UpdateMeasureConstraints(Size size)
    {
        if (Width.Unit is SizeUnit.Measured)
        {
            UpdateConstraints(ConstraintTypes.MeasuredWidth, _ => [_measuredWidth | Eq(Required) | size.Width]);
        }
        else
        {
            RemoveConstraints(ConstraintTypes.MeasuredWidth);
        }

        if (Height.Unit is SizeUnit.Measured)
        {
            UpdateConstraints(ConstraintTypes.MeasuredHeight, _ => [_measuredHeight | Eq(Required) | size.Height]);
        }
        else
        {
            RemoveConstraints(ConstraintTypes.MeasuredHeight);
        }
    }

    private void UpdatePolesConstraintsIfNeeded(IMagnetStage stage)
    {
        var margin = Margin;
        var collapsedMargin = CollapsedMargin;

        EnsureEffectiveLeftMargin(stage, collapsedMargin, margin);
        EnsureEffectiveRightMargin(stage, collapsedMargin, margin);
        EnsureEffectiveTopMargin(stage, collapsedMargin, margin);
        EnsureEffectiveBottomMargin(stage, collapsedMargin, margin);
    }

    private void EnsureEffectiveBottomMargin(IMagnetStage stage, Thickness collapsedMargin, Thickness margin)
    {
        if (BottomTo is { } bottomTo)
        {
            var bottomMargin = bottomTo.GetElement(stage) is IMagnetView { Visibility: Visibility.Collapsed } ? collapsedMargin.Bottom : margin.Bottom;
            if (_marginBottom != bottomMargin)
            {
                _marginBottom = bottomMargin;
                OnVerticalPropertyChanged();
            }
        }
        else
        {
            _marginBottom = 0;
        }
    }

    private void EnsureEffectiveTopMargin(IMagnetStage stage, Thickness collapsedMargin, Thickness margin)
    {
        if (TopTo is { } topTo)
        {
            var topMargin = topTo.GetElement(stage) is IMagnetView { Visibility: Visibility.Collapsed } ? collapsedMargin.Top : margin.Top;
            if (_marginTop != topMargin)
            {
                _marginTop = topMargin;
                OnVerticalPropertyChanged();
            }
        }
        else
        {
            _marginTop = 0;
        }
    }

    private void EnsureEffectiveRightMargin(IMagnetStage stage, Thickness collapsedMargin, Thickness margin)
    {
        if (RightTo is { } rightTo)
        {
            var rightMargin = rightTo.GetElement(stage) is IMagnetView { Visibility: Visibility.Collapsed } ? collapsedMargin.Right : margin.Right;
            if (_marginRight != rightMargin)
            {
                _marginRight = rightMargin;
                OnHorizontalPropertyChanged();
            }
        }
        else
        {
            _marginRight = 0;
        }
    }

    private void EnsureEffectiveLeftMargin(IMagnetStage stage, Thickness collapsedMargin, Thickness margin)
    {
        if (LeftTo is { } leftTo)
        {
            var leftMargin = leftTo.GetElement(stage) is IMagnetView { Visibility: Visibility.Collapsed } ? collapsedMargin.Left : margin.Left;
            if (_marginLeft != leftMargin)
            {
                _marginLeft = leftMargin;
                OnHorizontalPropertyChanged();
            }
        }
        else
        {
            _marginLeft = 0;
        }
    }

    private void OnLeftToChanged(HorizontalPullTarget? oldValue, HorizontalPullTarget? newValue) => OnHorizontalPropertyChanged();

    private void OnRightToChanged(HorizontalPullTarget? oldValue, HorizontalPullTarget? newValue) => OnHorizontalPropertyChanged();

    private void OnTopToChanged(VerticalPullTarget? oldValue, VerticalPullTarget? newValue) => OnVerticalPropertyChanged();

    private void OnBottomToChanged(VerticalPullTarget? oldValue, VerticalPullTarget? newValue) => OnVerticalPropertyChanged();

    private IEnumerable<Constraint> GetHorizontalConstraints(IMagnetStage stage)
    {
        var maybeLeftConnection = GetLeftConnection(stage);
        var maybeRightConnection = GetRightConnection(stage);

        if (maybeLeftConnection is { } lc)
        {
            yield return lc.Pole | lc.Relation | lc.ToPole;
            
            if (lc.ChainedView is not null)
            {
                yield return GetChainLeftConstraints();
            }
        }

        if (maybeRightConnection is { } rc)
        {
            yield return rc.Pole | rc.Relation | rc.ToPole;
            
            if (rc.ChainedView is not null)
            {
                yield return GetChainRightConstraints();
            }
        }

        if (maybeLeftConnection is { } leftConnection && maybeRightConnection is { } rightConnection)
        {
            if (Width is { Unit: SizeUnit.Constraint })
            {
                if (leftConnection.ChainedView is { } chainedView)
                {
                    // if we're part of the chain, we need to add the constraint to the previous sibling
                    yield return _constraintsWidth | Eq(Strong) | chainedView._constraintsWidth;
                }
                else if (rightConnection.ChainedView is null)
                {
                    // if we're not the chain head, we need to add the constraint
                    yield return _constraintsWidth | Eq(Strong) | (rightConnection.ToPole - leftConnection.ToPole);
                }
            }
            else if (leftConnection is { Traction: Traction.Strong, ChainedView: { } prev })
            {
                if (rightConnection.Traction == Traction.Strong)
                {
                    // We're part of the pack-chain, so we just need to propagate the information about the space from the tail to its right target
                    yield return _horizontalTailSpace | Eq(Required) | prev._horizontalTailSpace;
                }
                else
                {
                    // We're the pack-chain tail, so it's finally time to compute the space from me to the right target
                    yield return _horizontalTailSpace | Eq(Required) | rightConnection.ToPole - rightConnection.Pole;
                }
            }
            else if (rightConnection.Traction == Traction.Strong && leftConnection.Traction == Traction.Default && rightConnection.ChainedView is { } nextChainedView)
            {
                // We're the pack-chain head, so we need to apply the bias to the entire chain
                yield return (nextChainedView._horizontalTailSpace + (leftConnection.Pole - leftConnection.ToPole)) * HorizontalBias | Eq(Required) | (leftConnection.Pole - leftConnection.ToPole);
            }

            if (leftConnection.Traction == Traction.Default && rightConnection.Traction == Traction.Default)
            {
                yield return GetBiasConstraint(HorizontalBias, rightConnection.Pole, rightConnection.ToPole, leftConnection.Pole, leftConnection.ToPole);
            }
        }
    }

    private IEnumerable<Constraint> GetVerticalConstraints(IMagnetStage stage)
    {
        var maybeTopConnection = GetTopConnection(stage);
        var maybeBottomConnection = GetBottomConnection(stage);

        if (maybeTopConnection is { } tc)
        {
            yield return tc.Pole | tc.Relation | tc.ToPole;

            if (tc.ChainedView is not null)
            {
                yield return GetChainTopConstraints();
            }
        }

        if (maybeBottomConnection is { } bc)
        {
            yield return bc.Pole | bc.Relation | bc.ToPole;
            
            if (bc.ChainedView is not null)
            {
                yield return GetChainBottomConstraints();
            }
        }

        if (maybeTopConnection is { } topConnection && maybeBottomConnection is { } bottomConnection)
        {
            if (Width is { Unit: SizeUnit.Constraint })
            {
                if (topConnection.ChainedView is { } chainedView)
                {
                    // if we're part of the chain, we need to add the constraint to the previous sibling
                    yield return _constraintsHeight | Eq(Strong) | chainedView._constraintsHeight;
                }
                else if (bottomConnection.ChainedView is null)
                {
                    // if we're not the chain head, we need to add the constraint
                    yield return _constraintsHeight | Eq(Strong) | (bottomConnection.ToPole - topConnection.ToPole);
                }
            }
            else if (topConnection is { Traction: Traction.Strong, ChainedView: { } prev })
            {
                if (bottomConnection.Traction == Traction.Strong)
                {
                    // We're part of the pack-chain, so we just need to propagate the information about the space from the tail to its bottom target
                    yield return _verticalTailSpace | Eq(Required) | prev._verticalTailSpace;
                }
                else
                {
                    // We're the pack-chain tail, so it's finally time to compute the space from me to the bottom target
                    yield return _verticalTailSpace | Eq(Required) | bottomConnection.ToPole - bottomConnection.Pole;
                }
            }
            else if (bottomConnection.Traction == Traction.Strong && topConnection.Traction == Traction.Default && bottomConnection.ChainedView is { } nextChainedView)
            {
                // We're the pack-chain head, so we need to apply the bias to the entire chain
                yield return (nextChainedView._verticalTailSpace + (topConnection.Pole - topConnection.ToPole)) * VerticalBias | Eq(Required) | (topConnection.Pole - topConnection.ToPole);
            }

            if (topConnection.Traction == Traction.Default && bottomConnection.Traction == Traction.Default)
            {
                yield return GetBiasConstraint(VerticalBias, bottomConnection.Pole, bottomConnection.ToPole, topConnection.Pole, topConnection.ToPole);
            }
        }
    }

    private PoleConnection? GetLeftConnection(IMagnetStage stage)
    {
        PoleConnection? connection = null;
        if (LeftTo is { } leftTo)
        {
            var target = leftTo.GetElement(stage);
            var pole = Expression.From(_left);
            var traction = leftTo.Traction;
            var relation = traction == Traction.Default ? GreaterOrEq(Medium) : Eq(Medium);
            var toPole = (IsLeftPoleChained(target, out var chainedView) ? target.GetChainPole(leftTo.Pole) : target.GetPole(leftTo.Pole)) + _marginLeft;
            connection = (pole, traction, relation, toPole, chainedView);
        }

        return connection;
    }

    private PoleConnection? GetRightConnection(IMagnetStage stage)
    {
        PoleConnection? connection = null;
        if (RightTo is { } rightTo)
        {
            var target = rightTo.GetElement(stage);
            var pole = Expression.From(_right);
            var traction = rightTo.Traction;
            var relation = traction == Traction.Default ? LessOrEq(Medium) : Eq(Medium);
            var toPole = (IsRightPoleChained(target, out var chainedView) ? target.GetChainPole(rightTo.Pole) : target.GetPole(rightTo.Pole)) - _marginRight;
            connection = (pole, traction, relation, toPole, chainedView);
        }

        return connection;
    }

    private PoleConnection? GetTopConnection(IMagnetStage stage)
    {
        PoleConnection? connection = null;
        if (TopTo is { } topTo)
        {
            var target = topTo.GetElement(stage);
            var pole = Expression.From(_top);
            var traction = topTo.Traction;
            var relation = traction == Traction.Default ? GreaterOrEq(Medium) : Eq(Medium);
            var toPole = (IsTopPoleChained(target, out var chainedView) ? target.GetChainPole(topTo.Pole) : target.GetPole(topTo.Pole)) + _marginTop;
            connection = (pole, traction, relation, toPole, chainedView);
        }

        return connection;
    }

    private PoleConnection? GetBottomConnection(IMagnetStage stage)
    {
        PoleConnection? connection = null;
        if (BottomTo is { } bottomTo)
        {
            var target = bottomTo.GetElement(stage);
            var pole = Expression.From(_bottom);
            var traction = bottomTo.Traction;
            var relation = traction == Traction.Default ? LessOrEq(Medium) : Eq(Medium);
            var toPole = (IsBottomPoleChained(target, out var chainedView) ? target.GetChainPole(bottomTo.Pole) : target.GetPole(bottomTo.Pole)) - _marginBottom;
            connection = (pole, traction, relation, toPole, chainedView);
        }

        return connection;
    }

    private Constraint GetChainTopConstraints() => _chainTop | Eq(Required) | _top - _marginTop;
    private Constraint GetChainBottomConstraints() => _chainBottom | Eq(Required) | _bottom + _marginBottom;
    private Constraint GetChainLeftConstraints() => _chainLeft | Eq(Required) | _left - _marginLeft;
    private Constraint GetChainRightConstraints() => _chainRight | Eq(Required) | _right + _marginRight;

    private void OnWidthChanged(SizeValue oldValue, SizeValue newValue)
    {
        UpdateConstraints(ConstraintTypes.Width, GetWidthConstraints);

        if (oldValue.Unit != newValue.Unit)
        {
            OnHorizontalPropertyChanged();
        }
    }

    private void OnHeightChanged(SizeValue oldValue, SizeValue newValue)
    {
        UpdateConstraints(ConstraintTypes.Height, GetHeightConstraints);
        
        if (oldValue.Unit != newValue.Unit)
        {
            OnVerticalPropertyChanged();
        }
    }

    private IEnumerable<Constraint> GetWidthConstraints(IMagnetStage stage)
        => GetSizeConstraints(Width, _measuredWidth, _desiredWidth, _left, _right, _constraintsWidth, stage.Left, stage.Right, _bottom - _top);

    private IEnumerable<Constraint> GetHeightConstraints(IMagnetStage stage)
        => GetSizeConstraints(Height, _measuredHeight, _desiredHeight, _top, _bottom, _constraintsHeight, stage.Top, stage.Bottom, _right - _left);

    private void OnHorizontalPropertyChanged()
        => UpdateConstraints(ConstraintTypes.HorizontalPosition, GetHorizontalConstraints);

    private void OnVerticalPropertyChanged()
        => UpdateConstraints(ConstraintTypes.VerticalPosition, GetVerticalConstraints);

    private bool IsLeftPoleChained(IMagnetElementBase leftElement, [NotNullWhen(true)] out MagnetView? chainedView)
    {
        if (leftElement is MagnetView { RightTo: { Pole: HorizontalPoles.Left } rightToMe } maybeChainedView && rightToMe.Id == Id)
        {
            chainedView = maybeChainedView;
            return true;
        }

        chainedView = null;
        return false;
    }

    private bool IsRightPoleChained(IMagnetElementBase rightElement, [NotNullWhen(true)] out MagnetView? chainedView)
    {
        if (rightElement is MagnetView { LeftTo: { Pole: HorizontalPoles.Right } leftToMe } maybeChainedView && leftToMe.Id == Id)
        {
            chainedView = maybeChainedView;
            return true;
        }
        
        chainedView = null;
        return false;
    }

    private bool IsTopPoleChained(IMagnetElementBase topElement, [NotNullWhen(true)] out MagnetView? chainedView)
    {
        if (topElement is MagnetView { BottomTo: { Pole: VerticalPoles.Top } bottomToMe } maybeChainedView && bottomToMe.Id == Id)
        {
            chainedView = maybeChainedView;
            return true;
        }

        chainedView = null;
        return false;
    }

    private bool IsBottomPoleChained(IMagnetElementBase rightElement, [NotNullWhen(true)] out MagnetView? chainedView)
    {
        if (rightElement is MagnetView { TopTo: { Pole: VerticalPoles.Bottom } topToMe } maybeChainedView && topToMe.Id == Id)
        {
            chainedView = maybeChainedView;
            return true;
        }
        
        chainedView = null;
        return false;
    }

    private void OnHorizontalBiasChanged(double oldValue, double newValue)
        => OnHorizontalPropertyChanged();


    private void OnVerticalBiasChanged(double oldValue, double newValue)
        => OnVerticalPropertyChanged();

    private IEnumerable<Constraint> GetSizeConstraints(
        SizeValue size,
        Variable measured,
        Variable desired,
        Variable left,
        Variable right,
        Variable constraintSize,
        Variable sceneLeft,
        Variable sceneRight,
        Expression otherAxisSize
    )
    {
        var multiplier = size.Value;

        var relation = size.Behavior == SizeBehavior.Shrink ? LessOrEq(Weak) : Eq(Weak);

        switch (size.Unit)
        {
            case SizeUnit.Measured:
                yield return desired | Eq(Strong) | (measured * multiplier);
                yield return (right - left) | relation | desired;
                break;

            case SizeUnit.Constraint:
                yield return (right - left) | relation | (constraintSize * multiplier);
                break;

            case SizeUnit.Stage:
                yield return (right - left) | relation | ((sceneRight - sceneLeft) * multiplier);
                break;

            case SizeUnit.Ratio:
                yield return desired | Eq(Strong) | (otherAxisSize * multiplier);
                yield return (right - left) | relation | desired;
                break;

            default:
                throw new NotSupportedException();
        }
    }

    private static Constraint GetBiasConstraint(double bias, Expression end, Expression endTarget, Expression start, Expression startTarget)
        => bias switch
        {
            1d => end | Eq(Required) | endTarget,
            0d => start | Eq(Required) | startTarget,

            // start = startTarget + (endTarget - startTarget - size) * bias
            // start = startTarget + (endTarget - startTarget - end + start) * bias
            // 0 = startTarget + (endTarget - startTarget - end + start) * bias - start
            // 0 = startTarget + bias * endTarget - bias * startTarget - bias * end + bias * start - start
            // 0 = startTarget - bias * startTarget + bias * endTarget - bias * end + bias * start - start
            // 0 = startTarget * (1 - bias) + bias * endTarget - bias * end + start * (bias - 1)
            // bias * end = startTarget * (1 - bias) + bias * endTarget + start * (bias - 1)
            _ => (bias * end) | Eq(Required) | ((startTarget * (1 - bias)) + (endTarget * bias) + (start * (bias - 1)))
        };
}
