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
    /// <summary>Bindable property for <see cref="Direction" />.</summary>
    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(
        nameof(Direction),
        typeof(MagnetPole),
        typeof(MagnetBarrier),
        MagnetPole.Right,
        propertyChanged: OnStructurePropertyChanged
    );

    /// <summary>Bindable property for <see cref="Margin" />.</summary>
    public static readonly BindableProperty MarginProperty = BindableProperty.Create(
        nameof(Margin),
        typeof(double),
        typeof(MagnetBarrier),
        0d,
        propertyChanged: OnValuePropertyChanged
    );

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
        get => (MagnetPole) GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
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
        get => (double) GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
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

    /// <summary>Adds members given as views (carrying <c>Magnet.MagnetId</c>) or nodes (fluent).</summary>
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
    /// <summary>Bindable property for <see cref="Orientation" />.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
        nameof(Orientation),
        typeof(MagnetOrientation),
        typeof(MagnetGuideline),
        MagnetOrientation.Vertical,
        propertyChanged: OnStructurePropertyChanged
    );

    /// <summary>Bindable property for <see cref="Percent" />.</summary>
    public static readonly BindableProperty PercentProperty = BindableProperty.Create(
        nameof(Percent),
        typeof(double),
        typeof(MagnetGuideline),
        0d,
        propertyChanged: OnValuePropertyChanged
    );

    /// <summary>Bindable property for <see cref="Position" />.</summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(
        nameof(Position),
        typeof(double),
        typeof(MagnetGuideline),
        0d,
        propertyChanged: OnValuePropertyChanged
    );

    /// <summary>
    /// Gets or sets the orientation of the line.
    /// </summary>
    public MagnetOrientation Orientation
    {
        get => (MagnetOrientation) GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the fractional position (0..1) relative to the stage size. Animatable.
    /// </summary>
    public double Percent
    {
        get => (double) GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    /// <summary>
    /// Gets or sets the absolute offset (added to the <see cref="Percent" />-based position). Animatable.
    /// </summary>
    public double Position
    {
        get => (double) GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
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
    /// <summary>Bindable property for <see cref="Orientation" />.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
        nameof(Orientation),
        typeof(MagnetOrientation),
        typeof(MagnetChain),
        MagnetOrientation.Horizontal,
        propertyChanged: OnStructurePropertyChanged
    );

    /// <summary>Bindable property for <see cref="Style" />.</summary>
    public static readonly BindableProperty StyleProperty = BindableProperty.Create(
        nameof(Style),
        typeof(MagnetChainStyle),
        typeof(MagnetChain),
        MagnetChainStyle.Spread,
        propertyChanged: OnStructurePropertyChanged
    );

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
        get => (MagnetOrientation) GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
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
        get => (MagnetChainStyle) GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the weights of the members (positional, aligned with <see cref="Nodes" />, applies to <see cref="MagnetSizingUnit.Constraint" />-sized members only; missing entries default to 1).
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

    /// <summary>Adds members given as views (carrying <c>Magnet.MagnetId</c>) or nodes (fluent).</summary>
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
