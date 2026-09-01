namespace Nalu;

/// <summary>
/// Classifies a change applied to a <see cref="MagnetNode" /> or a <see cref="MagnetDefinition" />.
/// </summary>
[Flags]
public enum MagnetChange : byte
{
    /// <summary>
    /// Nothing changed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Only numeric values changed (margins, biases, sizes, percents, weights): the compiled layout is patched, not rebuilt.
    /// </summary>
    Values = 1,

    /// <summary>
    /// The constraint graph changed (targets, poles, units, nodes added/removed): the layout is recompiled.
    /// </summary>
    Structure = 2
}

/// <summary>
/// A side (pole) of a <see cref="MagnetNode" /> or of the <c>parent</c> stage.
/// </summary>
public enum MagnetPole : byte
{
    /// <summary>The left edge.</summary>
    Left,

    /// <summary>The right edge.</summary>
    Right,

    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The bottom edge.</summary>
    Bottom
}

/// <summary>
/// Orientation of a <see cref="MagnetGuideline" /> or a <see cref="MagnetChain" />.
/// </summary>
public enum MagnetOrientation : byte
{
    /// <summary>
    /// Horizontal: a horizontal chain lays members out along the X axis; a horizontal guideline is a horizontal line (constrains Y).
    /// </summary>
    Horizontal,

    /// <summary>
    /// Vertical: a vertical chain lays members out along the Y axis; a vertical guideline is a vertical line (constrains X).
    /// </summary>
    Vertical
}

/// <summary>
/// How a <see cref="MagnetSizing" /> is resolved.
/// </summary>
public enum MagnetSizingUnit : byte
{
    /// <summary>The view is measured and its desired size is used (optionally scaled by <see cref="MagnetSizing.Value" /> when &gt; 0).</summary>
    Measured,

    /// <summary>A fixed size in device-independent units (<see cref="MagnetSizing.Value" />).</summary>
    Fixed,

    /// <summary>The size fills the space between the two anchors of the axis (or the weighted share of a chain).</summary>
    Constraint,

    /// <summary>A fraction (<see cref="MagnetSizing.Value" />, 0..1) of the space between the two anchors of the axis.</summary>
    ConstraintPercent,

    /// <summary>The size is <see cref="MagnetSizing.Value" /> times the size of the other axis.</summary>
    Ratio,

    /// <summary>A fraction (<see cref="MagnetSizing.Value" />, 0..1) of the stage size on the same axis, regardless of the anchors.</summary>
    StagePercent
}

/// <summary>
/// Distribution style of a <see cref="MagnetChain" />.
/// </summary>
public enum MagnetChainStyle : byte
{
    /// <summary>Members are evenly spread, including space before the first and after the last member.</summary>
    Spread,

    /// <summary>First and last members stick to the chain ends; the remaining space is spread between members.</summary>
    SpreadInside,

    /// <summary>Members are packed together; the group is positioned inside the available space using the bias of the first member.</summary>
    Packed
}

/// <summary>
/// Declarative visibility action a <see cref="MagnetView" /> node applies to its bound view
/// (see <see cref="MagnetView.ApplyVisibility" />).
/// </summary>
public enum MagnetVisibilityAction : byte
{
    /// <summary>No opinion: the view's own <c>IsVisible</c> is left untouched.</summary>
    None,

    /// <summary>Sets <c>IsVisible = true</c> on the bound view when applied.</summary>
    Show,

    /// <summary>Sets <c>IsVisible = false</c> on the bound view when applied (animated as a fade-out inside a transition).</summary>
    Hide
}
