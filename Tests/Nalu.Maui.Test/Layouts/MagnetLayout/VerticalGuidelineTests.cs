using Nalu.Cassowary;
using Nalu.MagnetLayout;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class VerticalGuidelineTests
{
    [Fact]
    public void TestVerticalGuideline()
    {
        var solver = new Solver();
        var stage = Substitute.For<IMagnetStage>();
        var stageStart = new Variable();
        var stageEnd = new Variable();
        stage.Left.Returns(stageStart);
        stage.Right.Returns(stageEnd);
        stage.When(s => s.AddConstraint(Arg.Any<Constraint>()))
             .Do(call => solver.AddConstraint(call.Arg<Constraint>()));
        stage.When(s => s.RemoveConstraint(Arg.Any<Constraint>()))
             .Do(call => solver.RemoveConstraint(call.Arg<Constraint>()));
        solver.AddConstraint(stageStart | Eq(Required) | 0);
        solver.AddConstraint(stageEnd | Eq(Required) | 100);
        
        var guideline = new VerticalGuideline
                     {
                         FractionalPosition = 0.6,
                         Position = 5,
                     };
        
        guideline.SetStage(stage);
        guideline.ApplyConstraints();

        solver.ShouldHaveVariables(
            (guideline.Left, 65),
            (guideline.Right, 65)
        );
    }
}
