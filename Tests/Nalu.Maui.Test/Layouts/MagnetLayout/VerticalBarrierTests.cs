using Nalu.Cassowary;
using Nalu.MagnetLayout;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class VerticalBarrierTests
{
    [Theory]
    [InlineData(HorizontalPoles.Left, 10)] // Expect barrier at the minimum Start (10)
    [InlineData(HorizontalPoles.Right, 90)] // Expect barrier at the maximum End (90)
    public void TestVerticalBarrierPosition(HorizontalPoles pole, double expectedPosition)
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock Elements (implementing both IMagnetElement and IHorizontalPoles)
        var element1 = Substitute.For<IMagnetElement, IHorizontalPoles>();
        var element1Start = new Variable();
        var element1End = new Variable();
        element1.Id.Returns("element1");
        stage.GetElement("element1").Returns(element1); // Mock the stage to return the element
        ((IHorizontalPoles) element1).Left.Returns(element1Start); // Use interface property
        ((IHorizontalPoles) element1).Right.Returns(element1End); // Use interface property
        solver.AddConstraint(element1Start | Eq(Required) | 10);
        solver.AddConstraint(element1End | Eq(Required) | 40);

        var element2 = Substitute.For<IMagnetElement, IHorizontalPoles>();
        var element2Start = new Variable();
        var element2End = new Variable();
        element2.Id.Returns("element2");
        stage.GetElement("element2").Returns(element2); // Mock the GetElement method
        ((IHorizontalPoles) element2).Left.Returns(element2Start); // Use interface property
        ((IHorizontalPoles) element2).Right.Returns(element2End); // Use interface property
        solver.AddConstraint(element2Start | Eq(Required) | 60);
        solver.AddConstraint(element2End | Eq(Required) | 90);

        // Create Barrier
        var barrier = new VerticalBarrier
                      {
                          Pole = pole, // Use the theory parameter
                          Elements = ["element1", "element2"] // Provide the mocked elements
                      };

        // Act
        barrier.SetStage(stage); // This adds the barrier's constraints to the solver via the mocked stage
        barrier.ApplyConstraints();

        // Assert
        // Verify the barrier's Start and End positions using ShouldHaveVariables
        solver.ShouldHaveVariables(
            (barrier.Left, expectedPosition),
            (barrier.Right, expectedPosition) // End should be same as Start due to identity constraint
        );
    }

    [Theory]
    [InlineData(HorizontalPoles.Left, 5, 5)]
    [InlineData(HorizontalPoles.Left, 15, -5)]
    [InlineData(HorizontalPoles.Right, 85, -5)]
    [InlineData(HorizontalPoles.Right, 95, 5)]
    public void TestVerticalBarrierPositionWithMargin(HorizontalPoles pole, double expectedPosition, double margin)
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock Elements (implementing both IMagnetElement and IHorizontalPoles)
        var element1 = Substitute.For<IMagnetElement, IHorizontalPoles>();
        var element1Start = new Variable();
        var element1End = new Variable();
        element1.Id.Returns("element1");
        stage.GetElement("element1").Returns(element1); // Mock the stage to return the element
        ((IHorizontalPoles) element1).Left.Returns(element1Start); // Use interface property
        ((IHorizontalPoles) element1).Right.Returns(element1End); // Use interface property
        solver.AddConstraint(element1Start | Eq(Required) | 10);
        solver.AddConstraint(element1End | Eq(Required) | 40);

        var element2 = Substitute.For<IMagnetElement, IHorizontalPoles>();
        var element2Start = new Variable();
        var element2End = new Variable();
        element2.Id.Returns("element2");
        stage.GetElement("element2").Returns(element2); // Mock the GetElement method
        ((IHorizontalPoles) element2).Left.Returns(element2Start); // Use interface property
        ((IHorizontalPoles) element2).Right.Returns(element2End); // Use interface property
        solver.AddConstraint(element2Start | Eq(Required) | 60);
        solver.AddConstraint(element2End | Eq(Required) | 90);

        // Create Barrier
        var barrier = new VerticalBarrier
                      {
                          Pole = pole, // Use the theory parameter
                          Elements = ["element1", "element2"], // Provide the mocked elements
                          Margin = margin
                      };

        // Act
        barrier.SetStage(stage); // This adds the barrier's constraints to the solver via the mocked stage
        barrier.ApplyConstraints();

        // Assert
        // Verify the barrier's Start and End positions using ShouldHaveVariables
        solver.ShouldHaveVariables(
            (barrier.Left, expectedPosition),
            (barrier.Right, expectedPosition) // End should be same as Start due to identity constraint
        );
    }
}
