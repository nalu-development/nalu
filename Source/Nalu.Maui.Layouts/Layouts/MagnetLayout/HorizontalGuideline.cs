using Nalu.Cassowary;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.MagnetLayout;

/// <summary>
/// A horizontal guideline.
/// </summary>
public class HorizontalGuideline : GuidelineBase, IVerticalPoles
{
    /// <inheritdoc />
    protected override IEnumerable<Constraint> GetConstraintForPosition(IMagnetStage stage)
    {
        var fractionalPosition = FractionalPosition;

        if (fractionalPosition != 0)
        {
            var top = Position + stage.Top + (stage.Bottom - stage.Top) * fractionalPosition;
            yield return Top | Eq(Required) | top;
        }
        else
        {
            yield return stage.Top + Position | Eq(Required) | Top;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<Constraint> GetIdentityConstraint(IMagnetStage stage)
    {
        yield return Bottom | Eq(Required) | Top;
    }

    /// <inheritdoc />
    protected override void SetVariableNames(string id)
    {
        Top.SetName($"{id}.Top");
        Bottom.SetName($"{id}.Bottom");
    }

    /// <inheritdoc />
    public Variable Top { get; } = new();

    /// <inheritdoc />
    public Variable Bottom { get; } = new();
}
