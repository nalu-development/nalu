using System.ComponentModel;
using System.Globalization;

namespace Nalu;

/// <summary>
/// A virtual line placed at the outermost <see cref="Direction" /> pole of a set of nodes.
/// </summary>
/// <remarks>
/// A <see cref="MagnetPole.Left" />/<see cref="MagnetPole.Right" /> barrier is a vertical line (usable as a horizontal anchor target);
/// a <see cref="MagnetPole.Top" />/<see cref="MagnetPole.Bottom" /> barrier is a horizontal line. Collapsed members are ignored.
/// </remarks>
[ContentProperty(nameof(Nodes))]
public sealed class MagnetBarrier : MagnetNode
{
    internal const uint DirectionBit = 1u << 0;
    internal const uint MarginBit = 1u << 1;

    private MagnetPole _direction = MagnetPole.Right;
    private double _margin;

    private readonly IList<string> _nodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagnetBarrier" /> class.
    /// </summary>
    public MagnetBarrier()
    {
        _nodes = CreateStructureList<string>();
    }

    /// <summary>
    /// Gets or sets the pole of the members the barrier follows.
    /// </summary>
    public MagnetPole Direction
    {
        get => _direction;
        set => SetStructure(ref _direction, value, DirectionBit);
    }

    /// <summary>
    /// Gets or sets the identifiers of the member nodes (XAML: <c>&lt;x:String&gt;</c> items or a comma-separated attribute, <c>Nodes="a,b"</c>).
    /// </summary>
    /// <remarks>The setter replaces the contents: the backing list never changes identity.</remarks>
    [TypeConverter(typeof(MagnetNodeIdsTypeConverter))]
    public IList<string> Nodes
    {
        get => _nodes;
        set => ReplaceListContents(_nodes, value);
    }

    /// <summary>
    /// Gets or sets a margin pushing the barrier outward (positive values move a Right/Bottom barrier further right/down, a Left/Top barrier further left/up).
    /// </summary>
    public double Margin
    {
        get => _margin;
        set => SetValues(ref _margin, value, MarginBit);
    }

    /// <summary>Adds members (fluent).</summary>
    public MagnetBarrier With(params string[] elements)
    {
        foreach (var element in elements)
        {
            Nodes.Add(element);
        }

        return this;
    }

    /// <summary>Adds members given as nodes (fluent).</summary>
    public MagnetBarrier With(params MagnetNode[] elements)
    {
        foreach (var element in elements)
        {
            Nodes.Add(MagnetView.IdOf(element));
        }

        return this;
    }

    /// <summary>Adds members given as views carrying <c>Magnet.MagnetId</c> (fluent).</summary>
    public MagnetBarrier With(params BindableObject[] elements)
    {
        foreach (var element in elements)
        {
            Nodes.Add(MagnetView.IdOf(element));
        }

        return this;
    }
}

/// <summary>
/// A virtual line positioned relative to the stage.
/// </summary>
/// <remarks>
/// A vertical guideline is positioned along X (<c>x = stageWidth × Percent + Position</c>) and can be used as a horizontal anchor target;
/// a horizontal guideline is positioned along Y. <see cref="Position" /> is added to the <see cref="Percent" />-based position.
/// </remarks>
public sealed class MagnetGuideline : MagnetNode
{
    internal const uint OrientationBit = 1u << 0;
    internal const uint PercentBit = 1u << 1;
    internal const uint PositionBit = 1u << 2;

    private MagnetOrientation _orientation = MagnetOrientation.Vertical;
    private double _percent;
    private double _position;

    /// <summary>
    /// Gets or sets the orientation of the line.
    /// </summary>
    public MagnetOrientation Orientation
    {
        get => _orientation;
        set => SetStructure(ref _orientation, value, OrientationBit);
    }

    /// <summary>
    /// Gets or sets the fractional position (0..1) relative to the stage size. Animatable.
    /// </summary>
    public double Percent
    {
        get => _percent;
        set => SetValues(ref _percent, value, PercentBit);
    }

    /// <summary>
    /// Gets or sets the absolute offset (added to the <see cref="Percent" />-based position). Animatable.
    /// </summary>
    public double Position
    {
        get => _position;
        set => SetValues(ref _position, value, PositionBit);
    }
}

/// <summary>
/// Lays a group of views out along one axis (explicit chain).
/// </summary>
/// <remarks>
/// The chain start is the first member's anchor on the axis start side (<c>LeftTo</c>/<c>TopTo</c>, defaults to the stage start),
/// the chain end is the last member's anchor on the axis end side (<c>RightTo</c>/<c>BottomTo</c>, defaults to the stage end).
/// Inner members must not carry anchors on the chain axis, except anchors to the adjacent member which only contribute their margin.
/// Members sized <see cref="MagnetSizingUnit.Constraint" /> share the remaining space according to <see cref="Weights" />.
/// </remarks>
[ContentProperty(nameof(Nodes))]
public sealed class MagnetChain : MagnetNode
{
    internal const uint OrientationBit = 1u << 0;
    internal const uint StyleBit = 1u << 1;
    internal const uint GapBit = 1u << 2;
    internal const uint GapModeBit = 1u << 3;

    private MagnetOrientation _orientation = MagnetOrientation.Horizontal;
    private MagnetChainStyle _style = MagnetChainStyle.Spread;
    private double _gap;
    private MagnetChainGapMode _gapMode = MagnetChainGapMode.Anchors;

    private readonly IList<string> _nodes;
    private readonly IList<double> _weights;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagnetChain" /> class.
    /// </summary>
    public MagnetChain()
    {
        _nodes = CreateStructureList<string>();
        _weights = CreateValuesList<double>();
    }

    /// <summary>
    /// Gets or sets the axis of the chain.
    /// </summary>
    public MagnetOrientation Orientation
    {
        get => _orientation;
        set => SetStructure(ref _orientation, value, OrientationBit);
    }

    /// <summary>
    /// Gets or sets the ordered identifiers of the member views (XAML: <c>&lt;x:String&gt;</c> items or a comma-separated attribute, <c>Nodes="a,b"</c>).
    /// </summary>
    /// <remarks>The setter replaces the contents: the backing list never changes identity.</remarks>
    [TypeConverter(typeof(MagnetNodeIdsTypeConverter))]
    public IList<string> Nodes
    {
        get => _nodes;
        set => ReplaceListContents(_nodes, value);
    }

    /// <summary>
    /// Gets or sets the distribution style.
    /// </summary>
    public MagnetChainStyle Style
    {
        get => _style;
        set => SetStructure(ref _style, value, StyleBit);
    }

    /// <summary>
    /// Gets or sets the uniform gap placed between consecutive VISIBLE members (separator semantics:
    /// a collapsed member takes its gap away — no gone margins to think about). Animatable.
    /// </summary>
    /// <remarks>
    /// Applies to member pairs with no adjacent-member anchors: declaring an anchor to the adjacent member
    /// (<c>After="a,8"</c>) overrides the chain gap for that pair with the per-anchor margin/gone semantics.
    /// </remarks>
    public double Gap
    {
        get => _gap;
        set
        {
            MarkSet(GapBit);

            if (_gap == value)
            {
                return;
            }

            // Zero is structural: the compiler omits the gap ops entirely for a zero gap, so crossing
            // 0 <-> non-zero recompiles (and is part of the tape fingerprint); other changes just patch.
            var change = _gap == 0 != (value == 0) ? MagnetChange.Structure : MagnetChange.Values;
            _gap = value;
            OnPropertyChanged();
            Notify(change);
        }
    }

    /// <summary>
    /// Gets or sets how the margins between members are interpreted. Defaults to
    /// <see cref="MagnetChainGapMode.Anchors" /> (ConstraintLayout semantics); with
    /// <see cref="MagnetChainGapMode.Separators" /> the head/tail margins belong to the chain and survive their
    /// member collapsing, and inner margins apply only between visible members (a StackLayout-like padding+spacing
    /// model — the first visible member sits at the chain's leading margin, whichever member it is).
    /// </summary>
    public MagnetChainGapMode GapMode
    {
        get => _gapMode;
        set => SetStructure(ref _gapMode, value, GapModeBit);
    }

    /// <summary>
    /// Gets or sets the weights of the members (positional, aligned with <see cref="Nodes" />, applies to <see cref="MagnetSizingUnit.Constraint" />-sized members only; missing entries default to 1).
    /// Collapsed members are excluded: their share is redistributed to the visible weighted members.
    /// XAML: a comma-separated attribute, <c>Weights="2,1"</c>.
    /// </summary>
    /// <remarks>The setter replaces the contents: the backing list never changes identity.</remarks>
    [TypeConverter(typeof(MagnetWeightsTypeConverter))]
    public IList<double> Weights
    {
        get => _weights;
        set => ReplaceListContents(_weights, value);
    }

    /// <summary>Adds members (fluent).</summary>
    public MagnetChain With(params string[] elements)
    {
        foreach (var element in elements)
        {
            Nodes.Add(element);
        }

        return this;
    }

    /// <summary>Adds members given as nodes (fluent).</summary>
    public MagnetChain With(params MagnetNode[] elements)
    {
        foreach (var element in elements)
        {
            Nodes.Add(MagnetView.IdOf(element));
        }

        return this;
    }

    /// <summary>Adds members given as views carrying <c>Magnet.MagnetId</c> (fluent).</summary>
    public MagnetChain With(params BindableObject[] elements)
    {
        foreach (var element in elements)
        {
            Nodes.Add(MagnetView.IdOf(element));
        }

        return this;
    }
}

/// <summary>
/// Converts a comma-separated string of node identifiers (<c>"a, b"</c>) for <see cref="MagnetBarrier.Nodes" /> and <see cref="MagnetChain.Nodes" />.
/// </summary>
public class MagnetNodeIdsTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string ids ? ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : throw new NotSupportedException();

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is IEnumerable<string> ids ? string.Join(", ", ids) : throw new NotSupportedException();
}

/// <summary>
/// Converts a comma-separated string of weights (<c>"2, 1"</c>) for <see cref="MagnetChain.Weights" />.
/// </summary>
public class MagnetWeightsTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string weights
            ? weights.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(w => double.Parse(w, CultureInfo.InvariantCulture)).ToArray()
            : throw new NotSupportedException();

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is IEnumerable<double> weights ? string.Join(", ", weights.Select(w => w.ToString(CultureInfo.InvariantCulture))) : throw new NotSupportedException();
}
