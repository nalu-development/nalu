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
            var start = Position + stage.Left + ((stage.Right - stage.Left) * fractionalPosition);

            yield return Left | Eq(Required) | start;
        }
        else
        {
            yield return (stage.Left + Position) | Eq(Required) | Left;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<Constraint> GetIdentityConstraint(IMagnetStage stage)
    {
        yield return Right | Eq(Required) | Left;
    }

    /// <inheritdoc />
    protected override void SetVariableNames(string id)
    {
        Left.SetName($"{id}.Left");
        Right.SetName($"{id}.Right");
    }

    /// <inheritdoc />
    public Variable Left { get; } = new();

    /// <inheritdoc />
    public Variable Right { get; } = new();
}
