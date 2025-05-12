using Nalu.Cassowary;
using Nalu.MagnetLayout;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class HorizontalBarrierTests
{
    [Theory]
    [InlineData(VerticalPoles.Top, 10)] // Expect barrier at the minimum Top (10)
    [InlineData(VerticalPoles.Bottom, 90)] // Expect barrier at the maximum Bottom (90)
    public void TestHorizontalBarrierPosition(VerticalPoles pole, double expectedPosition)
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock Elements (implementing both IMagnetElement and IVerticalPoles)
        var element1 = Substitute.For<IMagnetElement, IVerticalPoles>();
        var element1Top = new Variable();
        var element1Bottom = new Variable();
        element1.Id.Returns("element1");
        stage.GetElement("element1").Returns(element1); // Mock the stage to return the element
        ((IVerticalPoles) element1).Top.Returns(element1Top); // Use interface property
        ((IVerticalPoles) element1).Bottom.Returns(element1Bottom); // Use interface property
        solver.AddConstraint(element1Top | Eq(Required) | 10);
        solver.AddConstraint(element1Bottom | Eq(Required) | 40);

        var element2 = Substitute.For<IMagnetElement, IVerticalPoles>();
        var element2Top = new Variable();
        var element2Bottom = new Variable();
        element2.Id.Returns("element2");
        stage.GetElement("element2").Returns(element2); // Mock the GetElement method
        ((IVerticalPoles) element2).Top.Returns(element2Top); // Use interface property
        ((IVerticalPoles) element2).Bottom.Returns(element2Bottom); // Use interface property
        solver.AddConstraint(element2Top | Eq(Required) | 60);
        solver.AddConstraint(element2Bottom | Eq(Required) | 90);

        // Create Barrier
        var barrier = new HorizontalBarrier
                      {
                          Pole = pole, // Use the theory parameter
                          Elements = ["element1", "element2"] // Provide the mocked elements
                      };

        // Act
        barrier.SetStage(stage); // This adds the barrier's constraints to the solver via the mocked stage
        barrier.ApplyConstraints();

        // Assert
        // Verify the barrier's Top and Bottom positions using ShouldHaveVariables
        solver.ShouldHaveVariables(
            (barrier.Top, expectedPosition),
            (barrier.Bottom, expectedPosition) // Bottom should be same as Top due to identity constraint
        );
    }

    [Theory]
    [InlineData(VerticalPoles.Top, 5, 5)]
    [InlineData(VerticalPoles.Top, 15, -5)]
    [InlineData(VerticalPoles.Bottom, 85, -5)]
    [InlineData(VerticalPoles.Bottom, 95, 5)]
    public void TestHorizontalBarrierPositionWithMargin(VerticalPoles pole, double expectedPosition, double margin)
    {
        // Arrange
        var solver = new Solver();

        // Mock Stage
        var stage = Substitute.For<IMagnetStage>();
        var stageTop = new Variable();
        var stageBottom = new Variable();
        stage.Top.Returns(stageTop);
        stage.Bottom.Returns(stageBottom);

        stage.When(s => s.AddConstraint(Arg.Any<Constraint>()))
             .Do(call => solver.AddConstraint(call.Arg<Constraint>()));

        stage.When(s => s.RemoveConstraint(Arg.Any<Constraint>()))
             .Do(call => solver.RemoveConstraint(call.Arg<Constraint>()));

        // Define stage boundaries
        solver.AddConstraint(stageTop | Eq(Required) | 0);
        solver.AddConstraint(stageBottom | Eq(Required) | 100);

        // Mock Elements (implementing both IMagnetElement and IVerticalPoles)
        var element1 = Substitute.For<IMagnetElement, IVerticalPoles>();
        var element1Top = new Variable();
        var element1Bottom = new Variable();
        element1.Id.Returns("element1");
        stage.GetElement("element1").Returns(element1); // Mock the stage to return the element
        ((IVerticalPoles) element1).Top.Returns(element1Top); // Use interface property
        ((IVerticalPoles) element1).Bottom.Returns(element1Bottom); // Use interface property
        solver.AddConstraint(element1Top | Eq(Required) | 10);
        solver.AddConstraint(element1Bottom | Eq(Required) | 40);

        var element2 = Substitute.For<IMagnetElement, IVerticalPoles>();
        var element2Top = new Variable();
        var element2Bottom = new Variable();
        element2.Id.Returns("element2");
        stage.GetElement("element2").Returns(element2); // Mock the GetElement method
        ((IVerticalPoles) element2).Top.Returns(element2Top); // Use interface property
        ((IVerticalPoles) element2).Bottom.Returns(element2Bottom); // Use interface property
        solver.AddConstraint(element2Top | Eq(Required) | 60);
        solver.AddConstraint(element2Bottom | Eq(Required) | 90);

        // Create Barrier
        var barrier = new HorizontalBarrier
                      {
                          Pole = pole, // Use the theory parameter
                          Elements = ["element1", "element2"], // Provide the mocked elements
                          Margin = margin
                      };

        // Act
        barrier.SetStage(stage); // This adds the barrier's constraints to the solver via the mocked stage
        barrier.ApplyConstraints();

        // Assert
        // Verify the barrier's Top and Bottom positions using ShouldHaveVariables
        solver.ShouldHaveVariables(
            (barrier.Top, expectedPosition),
            (barrier.Bottom, expectedPosition) // Bottom should be same as Top due to identity constraint
        );
    }
}
