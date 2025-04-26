using Nalu.Cassowary;
using Nalu.Internals;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.MagnetLayout;

/// <summary>
/// Given a group of <see cref="IMagnetElement" />s, this barrier will be positioned to the outermost <see cref="Pole" />.
/// </summary>
public class HorizontalBarrier : BarrierBase, IVerticalPoles
{
    /// <summary>
    /// The <see cref="BindableProperty" /> for the <see cref="Pole" /> property.
    /// </summary>
    public static readonly BindableProperty PoleProperty = GenericBindableProperty<HorizontalBarrier>.Create(
        nameof(Pole),
        VerticalPoles.Top,
        propertyChanged: b => b.OnPoleChanged
    );

    /// <summary>
    /// Gets or sets the <see cref="BarrierBase.Elements" /> pole to which the barrier will attach to.
    /// </summary>
    public VerticalPoles Pole
    {
        get => (VerticalPoles) GetValue(PoleProperty);
        set => SetValue(PoleProperty, value);
    }

    /// <inheritdoc />
    public Variable Top { get; } = new();

    /// <inheritdoc />
    public Variable Bottom { get; } = new();

    /// <inheritdoc />
    protected override void SetVariableNames(string id)
    {
        Top.SetName($"{id}.Top");
        Bottom.SetName($"{id}.Bottom");
    }

    /// <inheritdoc />
    protected override void OnElementsChanged(string[]? oldValue, string[]? newValue) => UpdateConstraints();

    /// <inheritdoc />
    protected override void OnMarginChanged(double oldValue, double newValue) => UpdateConstraints();

    private void OnPoleChanged(VerticalPoles oldValue, VerticalPoles newValue) => UpdateConstraints();

    private void UpdateConstraints()
    {
        EnsureConstraintsFactory(ConstraintTypes.Identity, GetIdentityConstraint);
        UpdateConstraints(ConstraintTypes.Position, GetPositionConstraint);
    }

    private IEnumerable<Constraint> GetIdentityConstraint(IMagnetStage stage)
    {
        yield return Bottom | Eq(Required) | Top;
    }

    private IEnumerable<Constraint> GetPositionConstraint(IMagnetStage stage)
    {
        var elements = Elements?.Select(stage.GetElement) ?? [];
        var pole = Pole;

        var weightedRelation = pole switch
        {
            VerticalPoles.Top => LessOrEq(Required),
            _ => GreaterOrEq(Required)
        };

        var margin = pole switch
        {
            VerticalPoles.Top => -Margin,
            _ => Margin
        };

        foreach (var element in elements)
        {
            yield return Top | weightedRelation | (element.GetPole(pole) + margin);
        }
    }
}
