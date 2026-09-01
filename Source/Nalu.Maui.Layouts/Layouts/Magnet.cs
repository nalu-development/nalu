using System.ComponentModel;
using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout.Engine;

namespace Nalu;

/// <summary>
/// A constraint-based <see cref="Layout" />: every child is positioned by anchoring its sides to other children,
/// virtual nodes (barriers, guidelines, chains) or the <c>parent</c> stage.
/// </summary>
public partial class Magnet : Layout, IMagnetOwner
{
    #region Bindable properties

    /// <summary>
    /// Bindable property for <see cref="Definition" />.
    /// </summary>
    public static readonly BindableProperty DefinitionProperty = BindableProperty.Create(
        nameof(Definition),
        typeof(MagnetDefinition),
        typeof(Magnet),
        propertyChanged: (b, o, n) => ((Magnet) b).OnDefinitionChanged((MagnetDefinition?) o, (MagnetDefinition?) n)
    );

    /// <summary>
    /// Bindable property for <see cref="PropagateMagnetIdToAutomationId" />.
    /// </summary>
    public static readonly BindableProperty PropagateMagnetIdToAutomationIdProperty = BindableProperty.Create(
        nameof(PropagateMagnetIdToAutomationId),
        typeof(bool),
        typeof(Magnet),
        true
    );

    /// <summary>
    /// Attached property holding the inline <see cref="MagnetView" /> of a child (created lazily by <see cref="GetConstraints" />).
    /// </summary>
    public static readonly BindableProperty ConstraintsProperty = BindableProperty.CreateAttached(
        "Constraints",
        typeof(MagnetView),
        typeof(Magnet),
        null
    );

    /// <summary>
    /// Attached property: the <see cref="MagnetNode.MagnetId" /> of a child.
    /// </summary>
    public static readonly BindableProperty MagnetIdProperty = BindableProperty.CreateAttached(
        "MagnetId",
        typeof(string),
        typeof(Magnet),
        null,
        propertyChanged: OnMagnetIdChanged
    );

    // ---- Set-only attached properties: commands that write into the child's MagnetView node. ----
    // They are never read back (use Magnet.GetConstraints(view)); the last set wins; clearing one (null / default)
    // removes the constraint it wrote only if the node still holds exactly that value.

    /// <summary>Attached (set-only): <see cref="MagnetView.LeftTo" /> of a child.</summary>
    public static readonly BindableProperty LeftToProperty = CreateAttachedAnchor("LeftTo", MagnetPole.Left);

    /// <summary>Attached (set-only): <see cref="MagnetView.RightTo" /> of a child.</summary>
    public static readonly BindableProperty RightToProperty = CreateAttachedAnchor("RightTo", MagnetPole.Right);

    /// <summary>Attached (set-only): <see cref="MagnetView.TopTo" /> of a child.</summary>
    public static readonly BindableProperty TopToProperty = CreateAttachedAnchor("TopTo", MagnetPole.Top);

    /// <summary>Attached (set-only): <see cref="MagnetView.BottomTo" /> of a child.</summary>
    public static readonly BindableProperty BottomToProperty = CreateAttachedAnchor("BottomTo", MagnetPole.Bottom);

    /// <summary>Attached (set-only): <see cref="MagnetView.WidthSizing" /> of a child.</summary>
    public static readonly BindableProperty WidthSizingProperty = CreateAttachedSizing("WidthSizing", MagnetOrientation.Horizontal);

    /// <summary>Attached (set-only): <see cref="MagnetView.HeightSizing" /> of a child.</summary>
    public static readonly BindableProperty HeightSizingProperty = CreateAttachedSizing("HeightSizing", MagnetOrientation.Vertical);

    /// <summary>Attached (set-only): <see cref="MagnetView.HorizontalBias" /> of a child.</summary>
    public static readonly BindableProperty HorizontalBiasProperty = CreateAttachedBias("HorizontalBias", MagnetOrientation.Horizontal);

    /// <summary>Attached (set-only): <see cref="MagnetView.VerticalBias" /> of a child.</summary>
    public static readonly BindableProperty VerticalBiasProperty = CreateAttachedBias("VerticalBias", MagnetOrientation.Vertical);

    /// <summary>Attached shortcut (set-only): <c>LeftTo = target.Right</c> — <c>"avatar,12"</c>, <c>"avatar,12,gone:0"</c>.</summary>
    public static readonly BindableProperty AfterProperty = CreateAttachedTarget("After", [(MagnetPole.Left, MagnetPole.Right)]);

    /// <summary>Attached shortcut (set-only): <c>RightTo = target.Left</c>.</summary>
    public static readonly BindableProperty BeforeProperty = CreateAttachedTarget("Before", [(MagnetPole.Right, MagnetPole.Left)]);

    /// <summary>Attached shortcut (set-only): <c>TopTo = target.Bottom</c>.</summary>
    public static readonly BindableProperty BelowProperty = CreateAttachedTarget("Below", [(MagnetPole.Top, MagnetPole.Bottom)]);

    /// <summary>Attached shortcut (set-only): <c>BottomTo = target.Top</c>.</summary>
    public static readonly BindableProperty AboveProperty = CreateAttachedTarget("Above", [(MagnetPole.Bottom, MagnetPole.Top)]);

    /// <summary>Attached shortcut (set-only): <c>LeftTo = target.Left</c>.</summary>
    public static readonly BindableProperty AlignLeftProperty = CreateAttachedTarget("AlignLeft", [(MagnetPole.Left, MagnetPole.Left)]);

    /// <summary>Attached shortcut (set-only): <c>RightTo = target.Right</c>.</summary>
    public static readonly BindableProperty AlignRightProperty = CreateAttachedTarget("AlignRight", [(MagnetPole.Right, MagnetPole.Right)]);

    /// <summary>Attached shortcut (set-only): <c>TopTo = target.Top</c>.</summary>
    public static readonly BindableProperty AlignTopProperty = CreateAttachedTarget("AlignTop", [(MagnetPole.Top, MagnetPole.Top)]);

    /// <summary>Attached shortcut (set-only): <c>BottomTo = target.Bottom</c>.</summary>
    public static readonly BindableProperty AlignBottomProperty = CreateAttachedTarget("AlignBottom", [(MagnetPole.Bottom, MagnetPole.Bottom)]);

    /// <summary>Attached shortcut (set-only): left and right anchored to the target (positioned by <c>HorizontalBias</c>).</summary>
    public static readonly BindableProperty HorizontallyWithinProperty = CreateAttachedTarget("HorizontallyWithin", [(MagnetPole.Left, MagnetPole.Left), (MagnetPole.Right, MagnetPole.Right)]);

    /// <summary>Attached shortcut (set-only): top and bottom anchored to the target (positioned by <c>VerticalBias</c>).</summary>
    public static readonly BindableProperty VerticallyWithinProperty = CreateAttachedTarget("VerticallyWithin", [(MagnetPole.Top, MagnetPole.Top), (MagnetPole.Bottom, MagnetPole.Bottom)]);

    /// <summary>Attached shortcut (set-only): all sides anchored to the target (positioned by the biases).</summary>
    public static readonly BindableProperty WithinProperty = CreateAttachedTarget("Within", [(MagnetPole.Left, MagnetPole.Left), (MagnetPole.Right, MagnetPole.Right), (MagnetPole.Top, MagnetPole.Top), (MagnetPole.Bottom, MagnetPole.Bottom)]);

    /// <summary>Attached shortcut (set-only): left and right anchored to the target and <c>WidthSizing="*"</c>.</summary>
    public static readonly BindableProperty FillWidthProperty = CreateAttachedTarget("FillWidth", [(MagnetPole.Left, MagnetPole.Left), (MagnetPole.Right, MagnetPole.Right)], fillAxis: MagnetOrientation.Horizontal);

    /// <summary>Attached shortcut (set-only): top and bottom anchored to the target and <c>HeightSizing="*"</c>.</summary>
    public static readonly BindableProperty FillHeightProperty = CreateAttachedTarget("FillHeight", [(MagnetPole.Top, MagnetPole.Top), (MagnetPole.Bottom, MagnetPole.Bottom)], fillAxis: MagnetOrientation.Vertical);

    /// <summary>
    /// Attached property holding the node currently bound to a child (inline or definition-declared).
    /// </summary>
    internal static readonly BindableProperty BoundNodeProperty = BindableProperty.CreateAttached(
        "BoundNode",
        typeof(MagnetView),
        typeof(Magnet),
        null
    );

    #endregion

    private readonly MagnetEngine _engine = new();
    private MagnetDefinition? _definition;
    private MagnetChange _dirty = MagnetChange.Structure;
    private bool _suppressNotifications;
    private MagnetChange _suppressedChanges;

    /// <summary>
    /// Gets or sets the definition holding virtual nodes and (optionally) view constraints. Created lazily when not assigned.
    /// </summary>
    public MagnetDefinition? Definition
    {
        get => (MagnetDefinition?) GetValue(DefinitionProperty);
        set => SetValue(DefinitionProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the <c>MagnetId</c> of a child is copied to its <c>AutomationId</c> when the latter is not set. Defaults to <c>true</c>.
    /// </summary>
    public bool PropagateMagnetIdToAutomationId
    {
        get => (bool) GetValue(PropagateMagnetIdToAutomationIdProperty);
        set => SetValue(PropagateMagnetIdToAutomationIdProperty, value);
    }

    /// <summary>
    /// Gets the engine (tests/diagnostics).
    /// </summary>
    internal MagnetEngine Engine => _engine;

    /// <summary>
    /// Gets the effective definition, creating an implicit one when needed.
    /// </summary>
    internal MagnetDefinition EffectiveDefinition
    {
        get
        {
            if (_definition is null)
            {
                Definition = new MagnetDefinition();
            }

            return _definition!;
        }
    }

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new MagnetLayoutManager(this);

    #region Attached property accessors

    /// <summary>
    /// Gets the inline constraints node of a view, creating it on first access.
    /// </summary>
    public static MagnetView GetConstraints(BindableObject view)
    {
        if (view.GetValue(ConstraintsProperty) is MagnetView node)
        {
            return node;
        }

        node = new MagnetView { Origin = MagnetNodeOrigin.View };

        if (GetMagnetId(view) is { } id)
        {
            node.MagnetId = id;
        }

        view.SetValue(ConstraintsProperty, node);

        if (view is Element { Parent: Magnet magnet } && view is IView iview && magnet.Contains(iview))
        {
            magnet.Bind(iview);
        }

        return node;
    }

    /// <summary>
    /// Gets the inline constraints node of a view without creating it.
    /// </summary>
    internal static MagnetView? TryGetConstraints(BindableObject view) => view.GetValue(ConstraintsProperty) as MagnetView;

    /// <summary>Gets the <c>MagnetId</c> attached property (the inline node's id when one exists).</summary>
    public static string? GetMagnetId(BindableObject view) => TryGetConstraints(view)?.MagnetId ?? (string?) view.GetValue(MagnetIdProperty);

    /// <summary>Sets the <c>MagnetId</c> attached property.</summary>
    public static void SetMagnetId(BindableObject view, string? value) => view.SetValue(MagnetIdProperty, value);

    /// <summary>Sets the <c>LeftTo</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetLeftTo(BindableObject view, MagnetAnchor? value) => view.SetValue(LeftToProperty, value);

    /// <summary>Sets the <c>RightTo</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetRightTo(BindableObject view, MagnetAnchor? value) => view.SetValue(RightToProperty, value);

    /// <summary>Sets the <c>TopTo</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetTopTo(BindableObject view, MagnetAnchor? value) => view.SetValue(TopToProperty, value);

    /// <summary>Sets the <c>BottomTo</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetBottomTo(BindableObject view, MagnetAnchor? value) => view.SetValue(BottomToProperty, value);

    /// <summary>Sets the <c>WidthSizing</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetWidthSizing(BindableObject view, MagnetSizing value) => view.SetValue(WidthSizingProperty, value);

    /// <summary>Sets the <c>HeightSizing</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetHeightSizing(BindableObject view, MagnetSizing value) => view.SetValue(HeightSizingProperty, value);

    /// <summary>Sets the <c>HorizontalBias</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetHorizontalBias(BindableObject view, double value) => view.SetValue(HorizontalBiasProperty, value);

    /// <summary>Sets the <c>VerticalBias</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetVerticalBias(BindableObject view, double value) => view.SetValue(VerticalBiasProperty, value);

    /// <summary>Sets the <c>After</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetAfter(BindableObject view, MagnetTarget? value) => view.SetValue(AfterProperty, value);

    /// <summary>Sets the <c>Before</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetBefore(BindableObject view, MagnetTarget? value) => view.SetValue(BeforeProperty, value);

    /// <summary>Sets the <c>Below</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetBelow(BindableObject view, MagnetTarget? value) => view.SetValue(BelowProperty, value);

    /// <summary>Sets the <c>Above</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetAbove(BindableObject view, MagnetTarget? value) => view.SetValue(AboveProperty, value);

    /// <summary>Sets the <c>AlignLeft</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetAlignLeft(BindableObject view, MagnetTarget? value) => view.SetValue(AlignLeftProperty, value);

    /// <summary>Sets the <c>AlignRight</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetAlignRight(BindableObject view, MagnetTarget? value) => view.SetValue(AlignRightProperty, value);

    /// <summary>Sets the <c>AlignTop</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetAlignTop(BindableObject view, MagnetTarget? value) => view.SetValue(AlignTopProperty, value);

    /// <summary>Sets the <c>AlignBottom</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetAlignBottom(BindableObject view, MagnetTarget? value) => view.SetValue(AlignBottomProperty, value);

    /// <summary>Sets the <c>HorizontallyWithin</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetHorizontallyWithin(BindableObject view, MagnetTarget? value) => view.SetValue(HorizontallyWithinProperty, value);

    /// <summary>Sets the <c>VerticallyWithin</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetVerticallyWithin(BindableObject view, MagnetTarget? value) => view.SetValue(VerticallyWithinProperty, value);

    /// <summary>Sets the <c>Within</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetWithin(BindableObject view, MagnetTarget? value) => view.SetValue(WithinProperty, value);

    /// <summary>Sets the <c>FillWidth</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetFillWidth(BindableObject view, MagnetTarget? value) => view.SetValue(FillWidthProperty, value);

    /// <summary>Sets the <c>FillHeight</c> attached property (set-only: read the node via <see cref="GetConstraints" />).</summary>
    public static void SetFillHeight(BindableObject view, MagnetTarget? value) => view.SetValue(FillHeightProperty, value);

    // Static getters exist only because XamlC resolves attached properties through them (they must be public, but XamlC
    // never calls them): hidden from IntelliSense and a compile error when called — read constraints via GetConstraints(view).
    private const string SetOnlyMessage = "Set-only attached property: read the constraints via Magnet.GetConstraints(view).";

    /// <summary>Not supported: <c>LeftTo</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetAnchorTypeConverter))]
    public static MagnetAnchor? GetLeftTo(BindableObject view) => null;

    /// <summary>Not supported: <c>RightTo</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetAnchorTypeConverter))]
    public static MagnetAnchor? GetRightTo(BindableObject view) => null;

    /// <summary>Not supported: <c>TopTo</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetAnchorTypeConverter))]
    public static MagnetAnchor? GetTopTo(BindableObject view) => null;

    /// <summary>Not supported: <c>BottomTo</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetAnchorTypeConverter))]
    public static MagnetAnchor? GetBottomTo(BindableObject view) => null;

    /// <summary>Not supported: <c>WidthSizing</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetSizingTypeConverter))]
    public static MagnetSizing GetWidthSizing(BindableObject view) => default;

    /// <summary>Not supported: <c>HeightSizing</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetSizingTypeConverter))]
    public static MagnetSizing GetHeightSizing(BindableObject view) => default;

    /// <summary>Not supported: <c>HorizontalBias</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    public static double GetHorizontalBias(BindableObject view) => 0.5;

    /// <summary>Not supported: <c>VerticalBias</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    public static double GetVerticalBias(BindableObject view) => 0.5;

    /// <summary>Not supported: <c>After</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetAfter(BindableObject view) => null;

    /// <summary>Not supported: <c>Before</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetBefore(BindableObject view) => null;

    /// <summary>Not supported: <c>Below</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetBelow(BindableObject view) => null;

    /// <summary>Not supported: <c>Above</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetAbove(BindableObject view) => null;

    /// <summary>Not supported: <c>AlignLeft</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetAlignLeft(BindableObject view) => null;

    /// <summary>Not supported: <c>AlignRight</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetAlignRight(BindableObject view) => null;

    /// <summary>Not supported: <c>AlignTop</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetAlignTop(BindableObject view) => null;

    /// <summary>Not supported: <c>AlignBottom</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetAlignBottom(BindableObject view) => null;

    /// <summary>Not supported: <c>HorizontallyWithin</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetHorizontallyWithin(BindableObject view) => null;

    /// <summary>Not supported: <c>VerticallyWithin</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetVerticallyWithin(BindableObject view) => null;

    /// <summary>Not supported: <c>Within</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetWithin(BindableObject view) => null;

    /// <summary>Not supported: <c>FillWidth</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetFillWidth(BindableObject view) => null;

    /// <summary>Not supported: <c>FillHeight</c> is set-only.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(SetOnlyMessage, true)]
    [TypeConverter(typeof(MagnetTargetTypeConverter))]
    public static MagnetTarget? GetFillHeight(BindableObject view) => null;

    internal static MagnetView? GetBoundNode(IView view) => view is BindableObject bo ? bo.GetValue(BoundNodeProperty) as MagnetView : null;

    private static BindableProperty CreateAttachedAnchor(string name, MagnetPole side)
        => BindableProperty.CreateAttached(
            name,
            typeof(MagnetAnchor?),
            typeof(Magnet),
            null,
            propertyChanged: (b, o, n) =>
            {
                var node = GetConstraints(b);
                var anchor = (MagnetAnchor?) n;

                // Apply a value; a clear removes the anchor only if nobody overwrote it in the meantime.
                if (anchor is not null || node.GetAnchor(side) == (MagnetAnchor?) o)
                {
                    node.SetAnchor(side, anchor);
                }
            }
        );

    private static BindableProperty CreateAttachedSizing(string name, MagnetOrientation axis)
        => BindableProperty.CreateAttached(
            name,
            typeof(MagnetSizing),
            typeof(Magnet),
            MagnetSizing.Measured,
            propertyChanged: (b, o, n) =>
            {
                var node = GetConstraints(b);
                var sizing = (MagnetSizing) n;
                var current = axis == MagnetOrientation.Horizontal ? node.WidthSizing : node.HeightSizing;

                if (sizing != MagnetSizing.Measured || current == (MagnetSizing) o)
                {
                    if (axis == MagnetOrientation.Horizontal)
                    {
                        node.WidthSizing = sizing;
                    }
                    else
                    {
                        node.HeightSizing = sizing;
                    }
                }
            }
        );

    private static BindableProperty CreateAttachedBias(string name, MagnetOrientation axis)
        => BindableProperty.CreateAttached(
            name,
            typeof(double),
            typeof(Magnet),
            0.5,
            propertyChanged: (b, o, n) =>
            {
                var node = GetConstraints(b);
                var bias = (double) n;
                var current = axis == MagnetOrientation.Horizontal ? node.HorizontalBias : node.VerticalBias;

                if (bias != 0.5 || current == (double) o)
                {
                    if (axis == MagnetOrientation.Horizontal)
                    {
                        node.HorizontalBias = bias;
                    }
                    else
                    {
                        node.VerticalBias = bias;
                    }
                }
            }
        );

    private static BindableProperty CreateAttachedTarget(string name, (MagnetPole Side, MagnetPole Pole)[] writes, MagnetOrientation? fillAxis = null)
        => BindableProperty.CreateAttached(
            name,
            typeof(MagnetTarget?),
            typeof(Magnet),
            null,
            propertyChanged: (b, o, n) =>
            {
                var node = GetConstraints(b);

                if (n is MagnetTarget target)
                {
                    foreach (var (side, pole) in writes)
                    {
                        node.SetAnchor(side, target.To(pole));
                    }

                    if (fillAxis == MagnetOrientation.Horizontal)
                    {
                        node.WidthSizing = MagnetSizing.Constraint;
                    }
                    else if (fillAxis == MagnetOrientation.Vertical)
                    {
                        node.HeightSizing = MagnetSizing.Constraint;
                    }
                }
                else if (o is MagnetTarget previous)
                {
                    // Clear: remove only what this shortcut wrote and nobody changed since.
                    foreach (var (side, pole) in writes)
                    {
                        if (node.GetAnchor(side) == previous.To(pole))
                        {
                            node.SetAnchor(side, null);
                        }
                    }

                    if (fillAxis == MagnetOrientation.Horizontal && node.WidthSizing == MagnetSizing.Constraint)
                    {
                        node.WidthSizing = MagnetSizing.Measured;
                    }
                    else if (fillAxis == MagnetOrientation.Vertical && node.HeightSizing == MagnetSizing.Constraint)
                    {
                        node.HeightSizing = MagnetSizing.Measured;
                    }
                }
            }
        );

    private static void OnMagnetIdChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var id = (string?) newValue;

        if (TryGetConstraints(bindable) is { } node)
        {
            node.MagnetId = id;
        }

        if (bindable is Element { Parent: Magnet magnet } && bindable is IView view && magnet.Contains(view))
        {
            magnet.Bind(view);
        }
    }

    #endregion

    #region Definition & binding

    private void OnDefinitionChanged(MagnetDefinition? oldValue, MagnetDefinition? newValue)
    {
        if (oldValue is not null)
        {
            foreach (var child in this)
            {
                Unbind(child, oldValue);
            }

            oldValue.Detach();
        }

        _definition = newValue;

        if (newValue is not null)
        {
            newValue.Attach(this);

            foreach (var child in this)
            {
                Bind(child);
            }
        }

        ((IMagnetOwner) this).OnNodeChanged(null, MagnetChange.Structure);
    }

    /// <summary>
    /// Binds a child to its node: inline node (transferred by reference into the definition) or definition node matched by id.
    /// </summary>
    internal void Bind(IView child)
    {
        if (child is not BindableObject bo)
        {
            return;
        }

        var definition = EffectiveDefinition;
        var inline = TryGetConstraints(bo);
        var id = GetMagnetId(bo);
        var previous = bo.GetValue(BoundNodeProperty) as MagnetView;

        MagnetView? node = null;

        if (inline is not null)
        {
            if (id is not null && !string.Equals(inline.MagnetId, id, StringComparison.Ordinal))
            {
                inline.MagnetId = id;
            }

            if (string.IsNullOrEmpty(inline.MagnetId))
            {
                throw new InvalidOperationException(
                    $"A child of Magnet has inline constraints but no MagnetId (set Magnet.MagnetId=\"...\" on the {child.GetType().Name})."
                );
            }

            definition.Register(inline, MagnetNodeOrigin.View);
            node = inline;
        }
        else if (id is not null)
        {
            if (definition.TryGet(id, out var declared))
            {
                if (declared is not MagnetView declaredView)
                {
                    throw new InvalidOperationException($"MagnetId '{id}' is a {declared.GetType().Name} and cannot be bound to a view.");
                }

                node = declaredView;
            }
            else
            {
                node = GetConstraints(bo);

                if (node.Definition is null)
                {
                    definition.Register(node, MagnetNodeOrigin.View);
                }
            }
        }

        if (previous is not null && !ReferenceEquals(previous, node))
        {
            if (ReferenceEquals(previous.View, child))
            {
                previous.View = null;
            }

            if (previous.Origin == MagnetNodeOrigin.View)
            {
                definition.Unregister(previous);
            }
        }

        bo.SetValue(BoundNodeProperty, node);

        if (node is null)
        {
            return;
        }

        if (!ReferenceEquals(node.View, child))
        {
            node.View = child;
            ((IMagnetOwner) this).OnNodeChanged(node, MagnetChange.Values);
        }

        PropagateAutomationId(bo, node.MagnetId);
    }

    private void Unbind(IView child, MagnetDefinition? definition)
    {
        if (child is not BindableObject bo)
        {
            return;
        }

        if (bo.GetValue(BoundNodeProperty) is MagnetView node)
        {
            if (ReferenceEquals(node.View, child))
            {
                node.View = null;
            }

            if (node.Origin == MagnetNodeOrigin.View)
            {
                (definition ?? node.Definition)?.Unregister(node);
            }

            bo.SetValue(BoundNodeProperty, null);
            ((IMagnetOwner) this).OnNodeChanged(node, MagnetChange.Values);
        }
    }

    private void PropagateAutomationId(BindableObject view, string? id)
    {
        if (id is null || !PropagateMagnetIdToAutomationId)
        {
            return;
        }

        if (view is Element element && string.IsNullOrEmpty(element.AutomationId))
        {
            element.AutomationId = id;
        }
    }

    /// <inheritdoc />
    protected override void OnAdd(int index, IView view)
    {
        base.OnAdd(index, view);
        Bind(view);
    }

    /// <inheritdoc />
    protected override void OnInsert(int index, IView view)
    {
        base.OnInsert(index, view);
        Bind(view);
    }

    /// <inheritdoc />
    protected override void OnRemove(int index, IView view)
    {
        Unbind(view, _definition);
        base.OnRemove(index, view);
    }

    /// <inheritdoc />
    protected override void OnUpdate(int index, IView view, IView oldView)
    {
        Unbind(oldView, _definition);
        base.OnUpdate(index, view, oldView);
        Bind(view);
    }

    /// <inheritdoc />
    protected override void OnClear()
    {
        foreach (var child in this)
        {
            Unbind(child, _definition);
        }

        base.OnClear();
    }

    #endregion

    #region Dirty tracking & compilation

    void IMagnetOwner.OnNodeChanged(MagnetNode? node, MagnetChange change)
    {
        if (change == MagnetChange.None)
        {
            return;
        }

        if (_suppressNotifications)
        {
            _suppressedChanges |= change;

            return;
        }

        if (_transition is not null)
        {
            OnExternalChangeDuringTransition(change);

            return;
        }

        if (_dirty == MagnetChange.None)
        {
            InvalidateMeasure();
        }

        _dirty |= change;
    }

    /// <summary>
    /// Consumes the dirty flags: recompiles or patches the tape.
    /// </summary>
    internal void EnsureCompiled()
    {
        var definition = EffectiveDefinition;

        if ((_dirty & MagnetChange.Structure) != 0 || !_engine.IsCompiled)
        {
            _engine.Compile(definition.AllNodesArray());
        }
        else if ((_dirty & MagnetChange.Values) != 0)
        {
            _engine.PatchValues();
        }

        _dirty = MagnetChange.None;
    }

    /// <summary>
    /// Gets whether the layout has any magnet node.
    /// </summary>
    internal bool HasNodes => _definition is { Count: > 0 };

    /// <summary>
    /// Gets or sets the maximum number of compiled layout structures kept in the process-wide cache. Defaults to 64.
    /// </summary>
    /// <remarks>
    /// Structurally identical definitions (e.g. template-instantiated cells) share one compiled entry, so the
    /// capacity counts distinct structures, not instances. It is a safety net against unbounded growth rather than
    /// a tuning knob: an evicted structure is transparently recompiled in a few tens of microseconds. Shrinking
    /// trims the least recently used entries immediately; 0 disables caching.
    /// </remarks>
    public static int CompilationCacheCapacity
    {
        get => MagnetTapeCache.Capacity;
        set => MagnetTapeCache.Capacity = value;
    }

    #endregion
}
