using System.Runtime.CompilerServices;
using Nalu.MagnetLayout.Engine;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

/// <summary>
/// Turns on the engine's differential solver verification for the WHOLE suite: every Measure is re-checked
/// against a concrete phase-1 evaluation and every Arrange against an unconditional full re-solve
/// (see MagnetEngine.VerifySolver). Any solver fast path — present or future — that diverges from the
/// reference makes the test that exercised it fail.
/// </summary>
internal static class MagnetSolverVerification
{
    [ModuleInitializer]
    internal static void Enable() => MagnetEngine.VerifySolver = true;
}

/// <summary>
/// Boundary scenarios for the arrange fast paths: cases where the solution at the arrange size differs
/// structurally from the solution at the measured (hug) size — clamp branches flipping, barrier winners
/// changing, cross-axis ratio propagation, visibility changes, arranges without a preceding measure.
/// The differential verifier is the primary oracle; the explicit assertions pin the semantics.
/// </summary>
public class MagnetSolverBoundaryTests
{
    private const string P = MagnetAnchor.Parent;

    [Fact]
    public void WeightedClampEngagesOnlyAtTheArrangeWidth()
    {
        var h = new EngineHarness();
        h.View("a", 0, 20).Left(P).Top(P).Size(MagnetSizing.Constraint.WithBounds(max: 60), MagnetSizing.Fixed(20));
        h.View("b", 40, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b"));

        // At the measure width the weighted member stays under its Max; at the arrange width it clamps.
        h.Layout(90, 100, 300, 20);

        h.Frame("a").Width.Should().Be(60);
        h.Frame("b").Width.Should().Be(40);
    }

    [Fact]
    public void WeightedMinEngagesOnlyAtTheArrangeWidth()
    {
        var h = new EngineHarness();
        h.View("a", 0, 20).Left(P).Top(P).Size(MagnetSizing.Constraint.WithBounds(min: 30), MagnetSizing.Fixed(20));
        h.View("b", 40, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b"));

        // Wide measure (Min inactive), narrow arrange (Min binds).
        h.Layout(300, 100, 60, 20);

        h.Frame("a").Width.Should().Be(30);
    }

    [Fact]
    public void BarrierWinnerChangesBetweenMeasureAndArrange()
    {
        var h = new EngineHarness();
        // "a" is stage-dependent (spans up to the 50% guideline), "b" is fixed: the barrier's rightmost
        // view flips between the two depending on the arrange width.
        h.Add(new MagnetGuideline { MagnetId = "mid", Percent = 0.5 });
        h.View("a", 0, 20).Left(P).Top(P).Right("mid", MagnetPole.Left).Size(MagnetSizing.Constraint, MagnetSizing.Fixed(20));
        h.View("b", 80, 20).Left(P).Below("a");
        h.Add(new MagnetBarrier { MagnetId = "bar", Direction = MagnetPole.Right }.With("a", "b"));
        h.View("c", 10, 10).Top(P).After("bar");

        h.Layout(100, 100, 300, 100);

        // At 300 wide the guideline sits at 150: "a" (0..150) overtakes "b" (80) and carries the barrier.
        h.Frame("a").Width.Should().Be(150);
        h.Frame("c").X.Should().Be(150);
    }

    [Fact]
    public void RatioHeightFollowsTheArrangeWidth()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).HorizontallyWithin(P).Top(P).Size(MagnetSizing.Constraint, MagnetSizing.Ratio(0.5));

        h.Layout(100, 100, 200, 100);

        // Height = width / 2 at the ARRANGE width, not the measured one.
        h.Frame("a").ShouldBe(0, 0, 200, 100);
    }

    [Fact]
    public void OverconstrainedMeasureThenRoomyArrange()
    {
        var h = new EngineHarness();
        h.View("a", 0, 20).Left(P).Top(P).Size(MagnetSizing.Constraint.WithBounds(min: 40), MagnetSizing.Fixed(20));
        h.View("b", 0, 20).Right(P).Top(P).Size(MagnetSizing.Constraint.WithBounds(min: 40), MagnetSizing.Fixed(20));
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b"));

        // Measure with less room than the Mins require (both clamp), then arrange with plenty (none clamps).
        h.Layout(50, 100, 200, 20);

        h.Frame("a").Width.Should().Be(100);
        h.Frame("b").Width.Should().Be(100);
    }

    [Fact]
    public void ArrangeWithoutAPrecedingMeasure()
    {
        // Recycled-cell path: the engine arranges cold (HasMeasured is false), measuring children on the way.
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 30, 20).Top(P).After("a", 10);
        h.Compile();

        h.Engine.Arrange(200, 50, MeasurePass.All);

        h.Frame("a").ShouldBe(0, 0, 40, 20);
        h.Frame("b").ShouldBe(50, 0, 30, 20);
    }

    [Fact]
    public void VisibilityChangeBetweenMeasureAndArrangeIsPickedUpByAFullArrange()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 30, 20).Top(P).After("a", 10);

        h.Layout(200, 50, 200, 50);
        h.Frame("b").X.Should().Be(50);

        // Collapse "a" and arrange at a DIFFERENT size without re-measuring: the full arrange re-reads
        // visibility and must honor it (gone semantics on b's anchor).
        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Engine.Arrange(180, 50, MeasurePass.All);

        h.Frame("a").Width.Should().Be(0);
        h.Frame("b").X.Should().Be(10);
    }

    [Fact]
    public void ArrangeSmallerThanTheMeasuredHug()
    {
        var h = new EngineHarness();
        h.View("a", 120, 20).Left(P).Top(P);
        h.View("b", 60, 20).Top(P).After("a", 10);

        // Content hugs to 190; the parent grants only 150 (overflow is the caller's problem, not ours).
        h.Layout(300, 50, 150, 20);

        h.Frame("a").ShouldBe(0, 0, 120, 20);
        h.Frame("b").X.Should().Be(130);
    }

    [Fact]
    public void DeltaArrangeEngagesOnTheReusePathAndMatchesTheFullSolve()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 0, 20).Top(P).Size(MagnetSizing.Constraint, MagnetSizing.Fixed(20));
        h.View("c", 40, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Gap = 12 }.With("a", "b", "c"));

        var before = h.Engine.DeltaArrangesTaken;

        // The canonical fill flow: arrange at the measure ARG width, hug height — the delta fast path
        // must engage (the differential verifier cross-checks the frames against the full solve).
        h.Measure(400, 50);
        h.Engine.Arrange(400, 20, MeasurePass.Deferred);

        (h.Engine.DeltaArrangesTaken - before).Should().BeGreaterThan(0);
        h.Frame("b").ShouldBe(52, 0, 296, 20);
        h.Frame("c").X.Should().Be(360);

        // A fresh measure re-arms the path at the new width.
        h.Measure(500, 50);
        h.Engine.Arrange(500, 20, MeasurePass.Deferred);
        h.Frame("c").X.Should().Be(460);
    }

    [Fact]
    public void DeltaArrangeBailsWhenAClampBranchFlips()
    {
        var h = new EngineHarness();
        h.View("a", 0, 20).Left(P).Top(P).Size(MagnetSizing.Constraint.WithBounds(max: 60), MagnetSizing.Fixed(20));
        h.View("b", 40, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b"));

        // Hug leaves the weighted member under its Max; the arrange width would push it past: whatever
        // path runs must produce the clamped solution (the verifier compares against the full solve).
        h.Measure(300, 50);
        h.Engine.Arrange(300, 20, MeasurePass.Deferred);

        h.Frame("a").Width.Should().Be(60);
        h.Frame("b").Width.Should().Be(40);
    }

    [Fact]
    public void DeltaArrangeBailsOnVisibilityChange()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 30, 20).Top(P).After("a", 10);

        h.Measure(200, 50);
        h.Fake("a").Visibility = Visibility.Collapsed;
        var before = h.Engine.DeltaArrangesTaken;
        h.Engine.Arrange(200, 20, MeasurePass.Deferred);

        // The delta path must refuse to replay a solution captured with different visibility; the full
        // solve it falls back to keeps the STALE measured width under Deferred (pre-existing semantics:
        // MAUI always re-measures on a visibility change, so this flow never reaches production) — the
        // differential verifier asserts the fallback matches the reference full solve either way.
        (h.Engine.DeltaArrangesTaken - before).Should().Be(0);
        h.Frame("b").X.Should().Be(50);

        // The realistic follow-up (re-measure, then arrange) lands on the collapsed solution.
        h.Layout(200, 50, 200, 50);
        h.Frame("b").X.Should().Be(10);
    }

    [Fact]
    public void DeltaArrangeBailsOnCrossAxisRatio()
    {
        var h = new EngineHarness();
        // The chain hugs to 40 but fills the arrange width; "r" mirrors the weighted member's width and
        // derives its height from it (Ratio) — a Y result driven by an X-stage-dependent slot.
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 0, 20).Top(P).Size(MagnetSizing.Constraint, MagnetSizing.Fixed(20));
        h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b"));
        h.View("r", 0, 0).Below("a").AlignLeft("b").AlignRight("b").Size(MagnetSizing.Constraint, MagnetSizing.Ratio(0.5));

        var before = h.Engine.DeltaArrangesTaken;
        h.Measure(400, 400);
        h.Engine.Arrange(400, 200, MeasurePass.Deferred);

        // Y reads the X-stage-dependent width: the compile-time flag must route X moves to the full solve.
        (h.Engine.DeltaArrangesTaken - before).Should().Be(0);
        h.Frame("r").Width.Should().Be(360);
        h.Frame("r").Height.Should().Be(180);
    }

    [Fact]
    public void GapChainArrangedWiderThanMeasured()
    {
        var h = new EngineHarness();
        h.View("a", 40, 20).Left(P).Top(P);
        h.View("b", 0, 20).Top(P).Size(MagnetSizing.Constraint, MagnetSizing.Fixed(20));
        h.View("c", 40, 20).Right(P).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Gap = 12 }.With("a", "b", "c"));

        h.Layout(150, 50, 400, 20);

        // b absorbs everything between the gaps at the ARRANGE width.
        h.Frame("b").ShouldBe(52, 0, 296, 20);
        h.Frame("c").X.Should().Be(360);
    }
}
