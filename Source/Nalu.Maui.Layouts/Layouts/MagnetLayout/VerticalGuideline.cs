using Nalu.Cassowary;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.MagnetLayout;

/// <summary>
/// A vertical guideline.
/// </summary>
public class VerticalGuideline : GuidelineBase, IHorizontalPoles
{
    /// <inheritdoc />
    protected override IEnumerable<Constraint> GetConstraintForPosition(IMagnetStage stage)
    {
        var fractionalPosition = FractionalPosition;

        if (fractionalPosition != 0)
        {
            var start = Position + stage.Start + (stage.End - stage.Start) * fractionalPosition;
            yield return Start | Eq(Required) | start;
        }
        else
        {
            yield return stage.Start + Position | Eq(Required) | Start;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<Constraint> GetIdentityConstraint(IMagnetStage stage)
    {
        yield return End | Eq(Required) | Start;
    }

    /// <inheritdoc />
    protected override void SetVariableNames(string id)
    {
        Start.SetName($"{id}.Start");
        End.SetName($"{id}.End");
    }

    /// <inheritdoc />
    public Variable Start { get; } = new();

    /// <inheritdoc />
    public Variable End { get; } = new();
}
