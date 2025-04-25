using Nalu.Cassowary;
using Nalu.MagnetLayout;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class HorizontalGuidelineTests
{
    [Fact]
    public void TestHorizontalGuideline()
    {
        var solver = new Solver();
        var stage = Substitute.For<IMagnetStage>();
        var stageTop = new Variable();
        var stageBottom = new Variable();
        stage.Top.Returns(stageTop);
        stage.Bottom.Returns(stageBottom);
        stage.When(s => s.AddConstraint(Arg.Any<Constraint>()))
             .Do(call => solver.AddConstraint(call.Arg<Constraint>()));
        stage.When(s => s.RemoveConstraint(Arg.Any<Constraint>()))
             .Do(call => solver.RemoveConstraint(call.Arg<Constraint>()));
        solver.AddConstraint(stageTop | Eq(Required) | 40);
        solver.AddConstraint(stageBottom | Eq(Required) | 100);
        
        var guideline = new HorizontalGuideline
                     {
                         Id = "test",
                         FractionalPosition = 0.5,
                         Position = -10,
                     };
        
        guideline.SetStage(stage);

        solver.ShouldHaveVariables(
            (guideline.Top, 60),
            (guideline.Bottom, 60)
        );
    }
}
