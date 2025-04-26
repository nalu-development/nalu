using Nalu.Cassowary;
using Nalu.Internals;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.MagnetLayout;

/// <summary>
/// Given a group of <see cref="IMagnetElement" />s, this barrier will be positioned to the outermost <see cref="Pole" />.
/// </summary>
public class VerticalBarrier : BarrierBase, IHorizontalPoles
{
    /// <summary>
    /// The <see cref="BindableProperty" /> for the <see cref="Pole" /> property.
    /// </summary>
    public static readonly BindableProperty PoleProperty = GenericBindableProperty<VerticalBarrier>.Create(
        nameof(Pole),
        HorizontalPoles.Left,
        propertyChanged: b => b.OnPoleChanged
    );

    /// <summary>
    /// Gets or sets the <see cref="BarrierBase.Elements" /> pole to which the barrier will attach to.
    /// </summary>
    public HorizontalPoles Pole
    {
        get => (HorizontalPoles) GetValue(PoleProperty);
        set => SetValue(PoleProperty, value);
    }

    /// <inheritdoc />
    public Variable Left { get; } = new();

    /// <inheritdoc />
    public Variable Right { get; } = new();

    /// <inheritdoc />
    protected override void SetVariableNames(string id)
    {
        Left.SetName($"{id}.Left");
        Right.SetName($"{id}.Right");
    }

    /// <inheritdoc />
    protected override void OnElementsChanged(string[]? oldValue, string[]? newValue) => UpdateConstraints();

    /// <inheritdoc />
    protected override void OnMarginChanged(double oldValue, double newValue) => UpdateConstraints();

    private void OnPoleChanged(HorizontalPoles oldValue, HorizontalPoles newValue) => UpdateConstraints();

    private void UpdateConstraints()
    {
        EnsureConstraintsFactory(ConstraintTypes.Identity, GetIdentityConstraint);
        UpdateConstraints(ConstraintTypes.Position, GetPositionConstraint);
    }

    private IEnumerable<Constraint> GetIdentityConstraint(IMagnetStage stage)
    {
        yield return Right | Eq(Required) | Left;
    }

    private IEnumerable<Constraint> GetPositionConstraint(IMagnetStage stage)
    {
        var elements = Elements?.Select(stage.GetElement) ?? [];
        var pole = Pole;

        var weightedRelation = pole switch
        {
            HorizontalPoles.Left => LessOrEq(Required),
            _ => GreaterOrEq(Required)
        };

        var margin = pole switch
        {
            HorizontalPoles.Left => -Margin,
            _ => Margin
        };

        foreach (var element in elements)
        {
            yield return Left | weightedRelation | (element.GetPole(pole) + margin);
        }
    }
}
