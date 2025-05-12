using Nalu.MagnetLayout;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

public class MagnetViewTests
{
    [Fact]
    public void AutoSizeBasePositioningWorks()
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual view
        var view = Substitute.For<IView>();
        view.Visibility.Returns(Visibility.Visible);
        view.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(40, 20));

        // Create the view
        var magnetView = new MagnetView
                         {
                             Id = "view",
                             View = view,
                             Margin = new Thickness(10),
                             LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                             TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                         };

        // Act
        magnetView.SetStage(stage);
        magnetView.ApplyConstraints();
        solver.FetchChanges();

        magnetView.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        magnetView.GetFrame().Should().Be(new Rect(10, 10, 40, 20));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(0.5, 40)]
    [InlineData(0.25, 25)]
    [InlineData(1, 70)]
    public void AutoSizeBiasPositioningWorks(double bias, double expectedStart)
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual view
        var view = Substitute.For<IView>();
        view.Visibility.Returns(Visibility.Visible);
        view.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(20, 20));

        // Create the view
        var magnetView = new MagnetView
                         {
                             Id = "view",
                             View = view,
                             HorizontalBias = bias,
                             VerticalBias = bias,
                             Margin = new Thickness(10),
                             LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                             RightTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Right),
                             TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top),
                             BottomTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Bottom)
                         };

        // Act
        magnetView.SetStage(stage);
        magnetView.ApplyConstraints();
        solver.FetchChanges();

        magnetView.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        var frame = magnetView.GetFrame();
        frame.Should().Be(new Rect(expectedStart, expectedStart, 20, 20));
    }

    [Fact]
    public void StageBasePositioningWorks()
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual view
        var view = Substitute.For<IView>();
        view.Visibility.Returns(Visibility.Visible);
        view.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(40, 20));

        // Create the view
        var magnetView = new MagnetView
                         {
                             Id = "view",
                             View = view,
                             Width = SizeValue.StagePercent(50),
                             Height = SizeValue.StagePercent(50),
                             Margin = new Thickness(10),
                             LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                             TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                         };

        // Act
        magnetView.SetStage(stage);
        magnetView.ApplyConstraints();
        solver.FetchChanges();

        magnetView.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        magnetView.GetFrame().Should().Be(new Rect(10, 10, 50, 50));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(0.5, 30)]
    [InlineData(1, 50)]
    [InlineData(0.25, 12.5, 0)]
    [InlineData(0.75, 100 - 50 - 12.5, 0)]
    public void ConstraintBasePositioningWorks(double bias, double expected, double margin = 10)
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual view
        var view = Substitute.For<IView>();
        view.Visibility.Returns(Visibility.Visible);
        view.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(30, 20));

        // Create the view
        var magnetView = new MagnetView
                         {
                             Id = "view",
                             View = view,
                             Width = SizeValue.Constraint(0.5),
                             Height = SizeValue.Constraint(0.5),
                             HorizontalBias = bias,
                             VerticalBias = bias,
                             Margin = new Thickness(margin),
                             LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                             RightTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Right),
                             TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top),
                             BottomTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Bottom)
                         };

        // Act
        magnetView.SetStage(stage);
        magnetView.ApplyConstraints();
        solver.FetchChanges();

        magnetView.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        var frame = magnetView.GetFrame();
        var expectedSize = (100 - (margin * 2)) / 2;
        frame.Should().Be(new Rect(expected, expected, expectedSize, expectedSize));
    }

    [Theory]
    [InlineData("1", "2r", 40, 80)]
    [InlineData("1r", "1", 20, 20)]
    public void RatioBasePositioningWorks(string width, string height, double expectedWidth, double expectedHeight)
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual view
        var view = Substitute.For<IView>();
        view.Visibility.Returns(Visibility.Visible);
        view.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(40, 20));

        // Create the view
        var magnetView = new MagnetView
                         {
                             Id = "view",
                             View = view,
                             Width = width,
                             Height = height,
                             Margin = new Thickness(10),
                             LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                             TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                         };

        // Act
        magnetView.SetStage(stage);
        magnetView.ApplyConstraints();
        solver.FetchChanges();

        magnetView.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        magnetView.GetFrame().Should().Be(new Rect(10, 10, expectedWidth, expectedHeight));
    }

    [Fact]
    public void ChainBasePositioningWorks()
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual views
        var virtualView1 = Substitute.For<IView>();
        virtualView1.Visibility.Returns(Visibility.Visible);
        virtualView1.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(40, 20));
        var virtualView2 = Substitute.For<IView>();
        virtualView2.Visibility.Returns(Visibility.Visible);
        virtualView2.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(40, 20));

        // Create the view
        const string view1 = "view1";
        const string view2 = "view2";

        var magnetView1 = new MagnetView
                          {
                              Id = view1,
                              View = virtualView1,
                              Width = "*",
                              Margin = new Thickness(10),
                              LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                              RightTo = new HorizontalPullTarget(view2, HorizontalPoles.Left),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                          };

        var magnetView2 = new MagnetView
                          {
                              Id = view2,
                              View = virtualView2,
                              Width = "*",
                              Margin = new Thickness(10),
                              LeftTo = new HorizontalPullTarget(view1, HorizontalPoles.Right),
                              RightTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Right),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                          };

        stage.GetElement(view1).Returns(magnetView1);
        stage.GetElement(view2).Returns(magnetView2);

        // Act
        magnetView1.SetStage(stage);
        magnetView2.SetStage(stage);

        magnetView1.ApplyConstraints();
        magnetView2.ApplyConstraints();
        solver.FetchChanges();

        magnetView1.FinalizeConstraints();
        magnetView2.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        var frame1 = magnetView1.GetFrame();
        var frame2 = magnetView2.GetFrame();

        frame1.Should().Be(new Rect(10, 10, 30, 20));
        frame2.Should().Be(new Rect(60, 10, 30, 20));
    }

    [Fact]
    public void ChainSpreadBasePositioningWorks()
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 92, 100);

        // Mock the virtual views
        var virtualView1 = Substitute.For<IView>();
        virtualView1.Visibility.Returns(Visibility.Visible);
        virtualView1.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(20, 20));
        var virtualView2 = Substitute.For<IView>();
        virtualView2.Visibility.Returns(Visibility.Visible);
        virtualView2.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(20, 20));

        // Create the view
        const string view1 = "view1";
        const string view2 = "view2";

        var magnetView1 = new MagnetView
                          {
                              Id = view1,
                              View = virtualView1,
                              Margin = new Thickness(10),
                              LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                              RightTo = new HorizontalPullTarget(view2, HorizontalPoles.Left),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                          };

        var magnetView2 = new MagnetView
                          {
                              Id = view2,
                              View = virtualView2,
                              Margin = new Thickness(10),
                              LeftTo = new HorizontalPullTarget(view1, HorizontalPoles.Right),
                              RightTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Right),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                          };

        stage.GetElement(view1).Returns(magnetView1);
        stage.GetElement(view2).Returns(magnetView2);

        // Act
        magnetView1.SetStage(stage);
        magnetView2.SetStage(stage);

        magnetView1.ApplyConstraints();
        magnetView2.ApplyConstraints();
        solver.FetchChanges();

        magnetView1.FinalizeConstraints();
        magnetView2.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        var frame1 = magnetView1.GetFrame();
        var frame2 = magnetView2.GetFrame();

        frame1.Should().Be(new Rect(14, 10, 20, 20));
        frame2.Should().Be(new Rect(58, 10, 20, 20));
    }

    [Fact]
    public void ChainPackBasePositioningWorks()
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 200, 200);

        // Mock the virtual views
        var virtualView1 = Substitute.For<IView>();
        virtualView1.Visibility.Returns(Visibility.Visible);
        virtualView1.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(20, 20));
        var virtualView2 = Substitute.For<IView>();
        virtualView2.Visibility.Returns(Visibility.Visible);
        virtualView2.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(20, 20));

        // Create the view
        const string view1 = "view1";
        const string view2 = "view2";

        var magnetView1 = new MagnetView
                          {
                              Id = view1,
                              View = virtualView1,
                              Margin = new Thickness(10),
                              LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                              RightTo = new HorizontalPullTarget(view2, HorizontalPoles.Left, Traction.Strong),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top),
                              BottomTo = new VerticalPullTarget(view2, VerticalPoles.Top, Traction.Strong)
                          };

        var magnetView2 = new MagnetView
                          {
                              Id = view2,
                              View = virtualView2,
                              Margin = new Thickness(10),
                              LeftTo = new HorizontalPullTarget(view1, HorizontalPoles.Right, Traction.Strong),
                              RightTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Right),
                              TopTo = new VerticalPullTarget(view1, VerticalPoles.Bottom, Traction.Strong),
                              BottomTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Bottom)
                          };

        stage.GetElement(view1).Returns(magnetView1);
        stage.GetElement(view2).Returns(magnetView2);

        // Act
        magnetView1.SetStage(stage);
        magnetView2.SetStage(stage);

        magnetView1.ApplyConstraints();
        magnetView2.ApplyConstraints();
        solver.FetchChanges();

        magnetView1.FinalizeConstraints();
        magnetView2.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        var frame1 = magnetView1.GetFrame();
        var frame2 = magnetView2.GetFrame();

        frame1.Should().Be(new Rect(70, 70, 20, 20));
        frame2.Should().Be(new Rect(110, 110, 20, 20));
    }

    [Fact]
    public void MeasuredWidthIsLessImportantThanConstraints()
    {
        // Arrange
        var stage = MagnetTestingStage.Create(out var solver, 100, 100);

        // Mock the virtual views
        var virtualView1 = Substitute.For<IView>();
        virtualView1.Visibility.Returns(Visibility.Visible);
        virtualView1.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(200, 20));
        var virtualView2 = Substitute.For<IView>();
        virtualView2.Visibility.Returns(Visibility.Visible);
        virtualView2.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(new Size(20, 20));

        // Create the view
        const string view1 = "view1";
        const string view2 = "view2";

        var magnetView1 = new MagnetView
                          {
                              Id = view1,
                              View = virtualView1,
                              Width = "~",
                              LeftTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Left),
                              RightTo = new HorizontalPullTarget(view2, HorizontalPoles.Left),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                          };

        var magnetView2 = new MagnetView
                          {
                              Id = view2,
                              View = virtualView2,
                              LeftTo = new HorizontalPullTarget(view1, HorizontalPoles.Right, Traction.Strong),
                              RightTo = new HorizontalPullTarget(IMagnetStage.StageId, HorizontalPoles.Right),
                              TopTo = new VerticalPullTarget(IMagnetStage.StageId, VerticalPoles.Top)
                          };

        stage.GetElement(view1).Returns(magnetView1);
        stage.GetElement(view2).Returns(magnetView2);

        // Act
        magnetView1.SetStage(stage);
        magnetView2.SetStage(stage);

        magnetView1.ApplyConstraints();
        magnetView2.ApplyConstraints();
        solver.FetchChanges();

        magnetView1.FinalizeConstraints();
        magnetView2.FinalizeConstraints();
        solver.FetchChanges();

        // Assert
        var frame1 = magnetView1.GetFrame();
        var frame2 = magnetView2.GetFrame();

        frame1.Should().Be(new Rect(0, 0, 80, 20));
        frame2.Should().Be(new Rect(80, 0, 20, 20));
    }
}
