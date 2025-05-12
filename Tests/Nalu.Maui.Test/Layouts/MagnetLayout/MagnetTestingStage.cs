using Nalu.Cassowary;
using Nalu.MagnetLayout;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public static class MagnetTestingStage
{
    public static IMagnetStage Create(out Solver solver, double width, double height)
    {
        // Arrange
        var solverInstance = solver = new Solver();

        // Mock Stage
        var stage = Substitute.For<IMagnetStage>();
        var stageStart = new Variable();
        stageStart.SetName("Stage.Start");
        var stageEnd = new Variable();
        stageEnd.SetName("Stage.End");
        var stageTop = new Variable();
        stageTop.SetName("Stage.Top");
        var stageBottom = new Variable();
        stageBottom.SetName("Stage.Bottom");
        stage.Left.Returns(stageStart);
        stage.Right.Returns(stageEnd);
        stage.Top.Returns(stageTop);
        stage.Bottom.Returns(stageBottom);
        stage.WidthRequest.Returns(width);
        stage.HeightRequest.Returns(height);
        stage.GetElement(IMagnetStage.StageId).Returns(stage); // Mock the GetElement method
        stage.When(s => s.AddConstraint(Arg.Any<Constraint>()))
             .Do(call => solverInstance.AddConstraint(call.Arg<Constraint>()));
        stage.When(s => s.RemoveConstraint(Arg.Any<Constraint>()))
             .Do(call => solverInstance.RemoveConstraint(call.Arg<Constraint>()));

        // Mock variable management
        stage.When(s => s.AddEditVariable(Arg.Any<Variable>(), Arg.Any<double>()))
                .Do(call => solverInstance.AddEditVariable(call.Arg<Variable>(), call.Arg<double>()));
        stage.When(s => s.RemoveEditVariable(Arg.Any<Variable>()))
                .Do(call => solverInstance.RemoveEditVariable(call.Arg<Variable>()));
        stage.When(s => s.SuggestValue(Arg.Any<Variable>(), Arg.Any<double>()))
                .Do(call => solverInstance.SuggestValue(call.Arg<Variable>(), call.Arg<double>()));

        // Define stage boundaries and update its variables
        solverInstance.AddConstraint(stageStart | Eq(Required) | 0);
        solverInstance.AddConstraint(stageEnd | Eq(Required) | width);
        solverInstance.AddConstraint(stageTop | Eq(Required) | 0);
        solverInstance.AddConstraint(stageBottom | Eq(Required) | height);
        solverInstance.FetchChanges();

        return stage;
    }
}
