namespace Nalu;

/// <summary>
/// The constraints of a view inside a <see cref="Magnet" /> layout.
/// </summary>
/// <remarks>
/// The single source of truth for visibility is <see cref="IView.Visibility" /> of the bound view
/// (<c>IsVisible="False"</c> collapses the view, its size becomes 0 and anchors to it use <see cref="MagnetAnchor.GoneMargin" />).
/// <see cref="ApplyVisibility" /> is a declared, one-shot action STAMPED onto that property when the node is applied —
/// it is never read back and does not participate in the layout solve.
/// </remarks>
public sealed class MagnetView : MagnetNode
{
    private MagnetAnchor? _leftTo;
    private MagnetAnchor? _rightTo;
    private MagnetAnchor? _topTo;
    private MagnetAnchor? _bottomTo;
    private MagnetSizing _widthSizing = MagnetSizing.Measured;
    private MagnetSizing _heightSizing = MagnetSizing.Measured;
    private double _horizontalBias = 0.5;
    private double _verticalBias = 0.5;
    private MagnetVisibilityAction _applyVisibility;

    private void SetAnchorField(ref MagnetAnchor? field, MagnetAnchor? value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        var change = MagnetAnchor.Diff(field, value);
        field = value;
        OnPropertyChanged(propertyName);
        Notify(change);
    }

    private void SetSizeField(ref MagnetSizing field, MagnetSizing value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        var change = field.DiffWith(value);
        field = value;
        OnPropertyChanged(propertyName);
        Notify(change);
    }

    /// <summary>
    /// Gets or sets the anchor of the left side.
    /// </summary>
    public MagnetAnchor? LeftTo
    {
        get => _leftTo;
        set => SetAnchorField(ref _leftTo, value);
    }

    /// <summary>
    /// Gets or sets the anchor of the right side.
    /// </summary>
    public MagnetAnchor? RightTo
    {
        get => _rightTo;
        set => SetAnchorField(ref _rightTo, value);
    }

    /// <summary>
    /// Gets or sets the anchor of the top side.
    /// </summary>
    public MagnetAnchor? TopTo
    {
        get => _topTo;
        set => SetAnchorField(ref _topTo, value);
    }

    /// <summary>
    /// Gets or sets the anchor of the bottom side.
    /// </summary>
    public MagnetAnchor? BottomTo
    {
        get => _bottomTo;
        set => SetAnchorField(ref _bottomTo, value);
    }

    /// <summary>
    /// Gets or sets how the width is resolved. Defaults to <see cref="MagnetSizing.Measured" />.
    /// </summary>
    public MagnetSizing WidthSizing
    {
        get => _widthSizing;
        set => SetSizeField(ref _widthSizing, value);
    }

    /// <summary>
    /// Gets or sets how the height is resolved. Defaults to <see cref="MagnetSizing.Measured" />.
    /// </summary>
    public MagnetSizing HeightSizing
    {
        get => _heightSizing;
        set => SetSizeField(ref _heightSizing, value);
    }

    /// <summary>
    /// Gets or sets the horizontal bias (0 = left, 1 = right) used when both horizontal anchors are set and the view does not fill the span.
    /// Also used as the bias of a packed horizontal chain when this view is the chain head.
    /// </summary>
    public double HorizontalBias
    {
        get => _horizontalBias;
        set => SetValues(ref _horizontalBias, value);
    }

    /// <summary>
    /// Gets or sets the vertical bias (0 = top, 1 = bottom) used when both vertical anchors are set and the view does not fill the span.
    /// Also used as the bias of a packed vertical chain when this view is the chain head.
    /// </summary>
    public double VerticalBias
    {
        get => _verticalBias;
        set => SetValues(ref _verticalBias, value);
    }

    /// <summary>
    /// Gets or sets the visibility action applied to the bound view's <c>IsVisible</c> — when the definition attaches,
    /// when the view binds (late-added child) and when this value changes. <see cref="MagnetVisibilityAction.None" />
    /// (the default) leaves the view untouched.
    /// </summary>
    /// <remarks>
    /// One-shot writes with standard MAUI semantics: applying detaches any binding on the view's <c>IsVisible</c>.
    /// A view whose visibility is scene-managed is owned by <see cref="ApplyVisibility" /> — do not also bind its
    /// <c>IsVisible</c>. Inside <see cref="Magnet.TransitionToAsync(System.Action,uint,Easing?)" /> the write is
    /// deferred and animated (fade-out for <see cref="MagnetVisibilityAction.Hide" />); scenes do not auto-revert:
    /// each definition declares the state of the views it manages.
    /// </remarks>
    public MagnetVisibilityAction ApplyVisibility
    {
        get => _applyVisibility;
        set
        {
            if (_applyVisibility == value)
            {
                return;
            }

            _applyVisibility = value;
            OnPropertyChanged();
            RequestVisibilityApply();
        }
    }

    private IView? _view;

    /// <summary>
    /// The view bound to this node (by identifier match or inline transfer).
    /// </summary>
    internal IView? View
    {
        get => _view;
        set
        {
            if (ReferenceEquals(_view, value))
            {
                return;
            }

            _view = value;

            if (value is not null)
            {
                RequestVisibilityApply();
            }
        }
    }

    /// <summary>
    /// Routes a pending <see cref="ApplyVisibility" /> to the owner (which defers it during a transition) or applies it directly.
    /// </summary>
    private void RequestVisibilityApply()
    {
        if (ApplyVisibility == MagnetVisibilityAction.None || _view is null)
        {
            return;
        }

        if (Owner is Magnet magnet)
        {
            magnet.OnApplyVisibilityRequested(this);
        }
        else
        {
            ApplyVisibilityNow();
        }
    }

    /// <summary>
    /// Stamps <see cref="ApplyVisibility" /> onto the bound view's <c>IsVisible</c> (no-op for <see cref="MagnetVisibilityAction.None" />).
    /// </summary>
    internal void ApplyVisibilityNow()
    {
        if (_view is VisualElement ve && ApplyVisibility is not MagnetVisibilityAction.None)
        {
            ve.IsVisible = ApplyVisibility == MagnetVisibilityAction.Show;
        }
    }

    /// <summary>Gets the anchor of a side of this view.</summary>
    internal MagnetAnchor? GetAnchor(MagnetPole side)
        => side switch
        {
            MagnetPole.Left => LeftTo,
            MagnetPole.Right => RightTo,
            MagnetPole.Top => TopTo,
            _ => BottomTo
        };

    /// <summary>Sets the anchor of a side of this view.</summary>
    internal void SetAnchor(MagnetPole side, MagnetAnchor? anchor)
    {
        switch (side)
        {
            case MagnetPole.Left:
                LeftTo = anchor;

                break;

            case MagnetPole.Right:
                RightTo = anchor;

                break;

            case MagnetPole.Top:
                TopTo = anchor;

                break;

            default:
                BottomTo = anchor;

                break;
        }
    }

    #region Fluent API

    /// <summary>Sets <see cref="ApplyVisibility" /> (fluent).</summary>
    public MagnetView Visibility(MagnetVisibilityAction action)
    {
        ApplyVisibility = action;

        return this;
    }

    /// <summary>Sets <see cref="MagnetNode.MagnetId" />.</summary>
    public MagnetView Id(string magnetId)
    {
        MagnetId = magnetId;

        return this;
    }

    /// <summary>Anchors the left side.</summary>
    public MagnetView Left(string target, MagnetPole pole = MagnetPole.Left, double margin = 0, double? goneMargin = null)
    {
        LeftTo = new MagnetAnchor(target, pole, margin, goneMargin);

        return this;
    }

    /// <summary>Anchors the right side.</summary>
    public MagnetView Right(string target, MagnetPole pole = MagnetPole.Right, double margin = 0, double? goneMargin = null)
    {
        RightTo = new MagnetAnchor(target, pole, margin, goneMargin);

        return this;
    }

    /// <summary>Anchors the top side.</summary>
    public MagnetView Top(string target, MagnetPole pole = MagnetPole.Top, double margin = 0, double? goneMargin = null)
    {
        TopTo = new MagnetAnchor(target, pole, margin, goneMargin);

        return this;
    }

    /// <summary>Anchors the bottom side.</summary>
    public MagnetView Bottom(string target, MagnetPole pole = MagnetPole.Bottom, double margin = 0, double? goneMargin = null)
    {
        BottomTo = new MagnetAnchor(target, pole, margin, goneMargin);

        return this;
    }

    /// <summary>Anchors all four sides to the same node with the given margins.</summary>
    public MagnetView Fill(string target, Thickness margin)
        => Left(target, MagnetPole.Left, margin.Left)
           .Top(target, MagnetPole.Top, margin.Top)
           .Right(target, MagnetPole.Right, margin.Right)
           .Bottom(target, MagnetPole.Bottom, margin.Bottom)
           .Size(MagnetSizing.Constraint, MagnetSizing.Constraint);

    // --- Relative shortcuts (readable verbs, C# only: XAML uses LeftTo/RightTo/TopTo/BottomTo) ---

    /// <summary>Places this view after (to the right of) the target: <c>LeftTo = target.Right</c>.</summary>
    public MagnetView After(MagnetTarget target) => Left(target.Target, MagnetPole.Right, target.Margin, target.GoneMargin);

    /// <summary>Places this view before (to the left of) the target: <c>RightTo = target.Left</c>.</summary>
    public MagnetView Before(MagnetTarget target) => Right(target.Target, MagnetPole.Left, target.Margin, target.GoneMargin);

    /// <summary>Places this view below the target: <c>TopTo = target.Bottom</c>.</summary>
    public MagnetView Below(MagnetTarget target) => Top(target.Target, MagnetPole.Bottom, target.Margin, target.GoneMargin);

    /// <summary>Places this view above the target: <c>BottomTo = target.Top</c>.</summary>
    public MagnetView Above(MagnetTarget target) => Bottom(target.Target, MagnetPole.Top, target.Margin, target.GoneMargin);

    /// <summary>Aligns the left side with the target's left side: <c>LeftTo = target.Left</c>.</summary>
    public MagnetView AlignLeft(MagnetTarget target) => Left(target.Target, MagnetPole.Left, target.Margin, target.GoneMargin);

    /// <summary>Aligns the right side with the target's right side: <c>RightTo = target.Right</c>.</summary>
    public MagnetView AlignRight(MagnetTarget target) => Right(target.Target, MagnetPole.Right, target.Margin, target.GoneMargin);

    /// <summary>Aligns the top side with the target's top side: <c>TopTo = target.Top</c>.</summary>
    public MagnetView AlignTop(MagnetTarget target) => Top(target.Target, MagnetPole.Top, target.Margin, target.GoneMargin);

    /// <summary>Aligns the bottom side with the target's bottom side: <c>BottomTo = target.Bottom</c>.</summary>
    public MagnetView AlignBottom(MagnetTarget target) => Bottom(target.Target, MagnetPole.Bottom, target.Margin, target.GoneMargin);

    /// <summary>Anchors left and right to the target: the view is placed inside it by <see cref="HorizontalBias" /> (0.5 = centered).</summary>
    public MagnetView HorizontallyWithin(MagnetTarget target) => AlignLeft(target).AlignRight(target);

    /// <summary>Anchors top and bottom to the target: the view is placed inside it by <see cref="VerticalBias" /> (0.5 = centered).</summary>
    public MagnetView VerticallyWithin(MagnetTarget target) => AlignTop(target).AlignBottom(target);

    /// <summary>Anchors all four sides to the target: the view is placed inside it by the biases (0.5 = centered).</summary>
    public MagnetView Within(MagnetTarget target) => HorizontallyWithin(target).VerticallyWithin(target);

    /// <summary>Anchors left and right to the target and sets <c>Width="*"</c>.</summary>
    public MagnetView FillWidth(MagnetTarget target)
    {
        HorizontallyWithin(target);
        WidthSizing = MagnetSizing.Constraint;

        return this;
    }

    /// <summary>Anchors top and bottom to the target and sets <c>Height="*"</c>.</summary>
    public MagnetView FillHeight(MagnetTarget target)
    {
        VerticallyWithin(target);
        HeightSizing = MagnetSizing.Constraint;

        return this;
    }


    // --- String targets with margins ---

    /// <inheritdoc cref="After(MagnetTarget)" />
    public MagnetView After(string target, double margin, double? goneMargin = null) => After(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="Before(MagnetTarget)" />
    public MagnetView Before(string target, double margin, double? goneMargin = null) => Before(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="Below(MagnetTarget)" />
    public MagnetView Below(string target, double margin, double? goneMargin = null) => Below(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="Above(MagnetTarget)" />
    public MagnetView Above(string target, double margin, double? goneMargin = null) => Above(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="AlignLeft(MagnetTarget)" />
    public MagnetView AlignLeft(string target, double margin, double? goneMargin = null) => AlignLeft(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="AlignRight(MagnetTarget)" />
    public MagnetView AlignRight(string target, double margin, double? goneMargin = null) => AlignRight(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="AlignTop(MagnetTarget)" />
    public MagnetView AlignTop(string target, double margin, double? goneMargin = null) => AlignTop(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="AlignBottom(MagnetTarget)" />
    public MagnetView AlignBottom(string target, double margin, double? goneMargin = null) => AlignBottom(new MagnetTarget(target, margin, goneMargin));

    /// <inheritdoc cref="HorizontallyWithin(MagnetTarget)" />
    public MagnetView HorizontallyWithin(string target, double margin) => HorizontallyWithin(new MagnetTarget(target, margin));

    /// <inheritdoc cref="VerticallyWithin(MagnetTarget)" />
    public MagnetView VerticallyWithin(string target, double margin) => VerticallyWithin(new MagnetTarget(target, margin));

    /// <inheritdoc cref="Within(MagnetTarget)" />
    public MagnetView Within(string target, double margin) => Within(new MagnetTarget(target, margin));

    /// <inheritdoc cref="FillWidth(MagnetTarget)" />
    public MagnetView FillWidth(string target, double margin) => FillWidth(new MagnetTarget(target, margin));

    /// <inheritdoc cref="FillHeight(MagnetTarget)" />
    public MagnetView FillHeight(string target, double margin) => FillHeight(new MagnetTarget(target, margin));

    // --- Typed targets: a view carrying Magnet.MagnetId, or a node ---

    /// <summary>Resolves the <see cref="MagnetNode.MagnetId" /> of a view or node used as a target.</summary>
    public static string IdOf(object target)
    {
        var id = target switch
        {
            MagnetNode node => node.MagnetId,
            BindableObject bindable => Magnet.GetMagnetId(bindable),
            _ => null
        };

        return string.IsNullOrEmpty(id)
            ? throw new InvalidOperationException($"The target {target.GetType().Name} has no MagnetId (set Magnet.MagnetId on it, or use Magnet.GetConstraints(view).Id(...)).")
            : id;
    }

    /// <summary>Builds a <see cref="MagnetTarget" /> from a view or node.</summary>
    public static MagnetTarget TargetOf(object target, double margin = 0, double? goneMargin = null) => new(IdOf(target), margin, goneMargin);

    /// <inheritdoc cref="Left(string, MagnetPole, double, double?)" />
    public MagnetView Left(object target, MagnetPole pole = MagnetPole.Left, double margin = 0, double? goneMargin = null) => Left(IdOf(target), pole, margin, goneMargin);

    /// <inheritdoc cref="Right(string, MagnetPole, double, double?)" />
    public MagnetView Right(object target, MagnetPole pole = MagnetPole.Right, double margin = 0, double? goneMargin = null) => Right(IdOf(target), pole, margin, goneMargin);

    /// <inheritdoc cref="Top(string, MagnetPole, double, double?)" />
    public MagnetView Top(object target, MagnetPole pole = MagnetPole.Top, double margin = 0, double? goneMargin = null) => Top(IdOf(target), pole, margin, goneMargin);

    /// <inheritdoc cref="Bottom(string, MagnetPole, double, double?)" />
    public MagnetView Bottom(object target, MagnetPole pole = MagnetPole.Bottom, double margin = 0, double? goneMargin = null) => Bottom(IdOf(target), pole, margin, goneMargin);

    /// <inheritdoc cref="After(MagnetTarget)" />
    public MagnetView After(object target, double margin = 0, double? goneMargin = null) => After(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="Before(MagnetTarget)" />
    public MagnetView Before(object target, double margin = 0, double? goneMargin = null) => Before(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="Below(MagnetTarget)" />
    public MagnetView Below(object target, double margin = 0, double? goneMargin = null) => Below(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="Above(MagnetTarget)" />
    public MagnetView Above(object target, double margin = 0, double? goneMargin = null) => Above(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="AlignLeft(MagnetTarget)" />
    public MagnetView AlignLeft(object target, double margin = 0, double? goneMargin = null) => AlignLeft(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="AlignRight(MagnetTarget)" />
    public MagnetView AlignRight(object target, double margin = 0, double? goneMargin = null) => AlignRight(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="AlignTop(MagnetTarget)" />
    public MagnetView AlignTop(object target, double margin = 0, double? goneMargin = null) => AlignTop(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="AlignBottom(MagnetTarget)" />
    public MagnetView AlignBottom(object target, double margin = 0, double? goneMargin = null) => AlignBottom(TargetOf(target, margin, goneMargin));

    /// <inheritdoc cref="HorizontallyWithin(MagnetTarget)" />
    public MagnetView HorizontallyWithin(object target, double margin = 0) => HorizontallyWithin(TargetOf(target, margin));

    /// <inheritdoc cref="VerticallyWithin(MagnetTarget)" />
    public MagnetView VerticallyWithin(object target, double margin = 0) => VerticallyWithin(TargetOf(target, margin));

    /// <inheritdoc cref="Within(MagnetTarget)" />
    public MagnetView Within(object target, double margin = 0) => Within(TargetOf(target, margin));

    /// <inheritdoc cref="FillWidth(MagnetTarget)" />
    public MagnetView FillWidth(object target, double margin = 0) => FillWidth(TargetOf(target, margin));

    /// <inheritdoc cref="FillHeight(MagnetTarget)" />
    public MagnetView FillHeight(object target, double margin = 0) => FillHeight(TargetOf(target, margin));

    /// <summary>Sets width and height.</summary>
    public MagnetView Size(MagnetSizing width, MagnetSizing height)
    {
        WidthSizing = width;
        HeightSizing = height;

        return this;
    }

    /// <summary>Sets a fixed width and height.</summary>
    public MagnetView Size(double width, double height) => Size(MagnetSizing.Fixed(width), MagnetSizing.Fixed(height));

    /// <summary>Sets horizontal and vertical bias.</summary>
    public MagnetView Bias(double horizontal, double vertical)
    {
        HorizontalBias = horizontal;
        VerticalBias = vertical;

        return this;
    }

    #endregion
}
