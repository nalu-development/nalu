namespace Nalu.Maui.Test.Layouts.MagnetLayout;

/// <summary>
/// Chains with per-pair margins combined with member visibility: the margins must always follow the GONE rules
/// (a collapsed member drops its OWN margins; the anchor pointing AT a collapsed member uses its gone margin),
/// and weighted members must redistribute the share of collapsed ones.
/// </summary>
public class MagnetChainMarginTests
{
    private const string P = MagnetAnchor.Parent;

    /// <summary>Packed [a,b,c], widths 30, gaps declared on the adjacent anchors: 8 (gone 2) and 16 (gone 4).</summary>
    private static EngineHarness CreatePackedRow()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 8, goneMargin: 2).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 16, goneMargin: 4).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b", "c"));

        return h;
    }

    [Fact]
    public void GapsCanDifferPerPair()
    {
        var h = CreatePackedRow();

        h.Layout(200, 100, 200, 100);

        // Each gap is the margin of the adjacent-member anchor: 8 between a and b, 16 between b and c.
        h.Frame("a").ShouldBe(0, 0, 30, 20);
        h.Frame("b").ShouldBe(38, 0, 30, 20);
        h.Frame("c").ShouldBe(84, 0, 30, 20);
        (h.Frame("b").X - h.Frame("a").Right).Should().Be(8);
        (h.Frame("c").X - h.Frame("b").Right).Should().Be(16);
    }

    [Fact]
    public void HiddenMiddleMemberLeavesTheGoneMarginOfTheAnchorCrossingIt()
    {
        var h = CreatePackedRow();
        h.Fake("b").Visibility = Visibility.Collapsed;

        h.Layout(200, 100, 200, 100);

        // b's own margin (8) drops; c's anchor targets the collapsed b → gone margin 4.
        h.Frame("a").ShouldBe(0, 0, 30, 20);
        h.Frame("b").ShouldBe(30, 0, 0, 0);
        h.Frame("c").ShouldBe(34, 0, 30, 20);
    }

    [Fact]
    public void HiddenFirstMemberDropsItsStartMarginAndTheNextUsesTheGoneMargin()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 10).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 8, goneMargin: 2).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 16, goneMargin: 4).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b", "c"));
        h.Fake("a").Visibility = Visibility.Collapsed;

        h.Layout(200, 100, 200, 100);

        // The chain start is the collapsed head's anchor with its margin dropped (10 → 0);
        // b sits at the gone margin of its anchor to the collapsed a.
        h.Frame("a").ShouldBe(0, 0, 0, 0);
        h.Frame("b").ShouldBe(2, 0, 30, 20);
        h.Frame("c").ShouldBe(48, 0, 30, 20);
    }

    [Fact]
    public void HiddenLastMemberDropsItsEndMarginAndItsGap()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(1, 0.5); // packed to the END: the chain end matters
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 8, goneMargin: 2).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 16, goneMargin: 4).Right(P, margin: 12).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b", "c"));

        h.Layout(200, 100, 200, 100);
        // Sanity: packed to the end margin (200 - 12 - 30).
        h.Frame("c").ShouldBe(158, 0, 30, 20);

        h.Fake("c").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);

        // The collapsed tail drops its own end margin (12) and its own gap anchor (16):
        // the group hugs the very end of the chain.
        h.Frame("b").ShouldBe(170, 0, 30, 20);
        h.Frame("c").ShouldBe(200, 0, 0, 0);
        h.Frame("a").ShouldBe(132, 0, 30, 20);
    }

    [Fact]
    public void SpreadChainWithMarginsSkipsTheHiddenMemberButKeepsTheGoneMargin()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 8, goneMargin: 2).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 16, goneMargin: 4).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Spread }.With("a", "b", "c"));
        h.Fake("b").Visibility = Visibility.Collapsed;

        h.Layout(200, 100, 200, 100);

        // sizes 60, surviving gap 4 → slack 136, two visible members → spread gap 136/3.
        h.Frame("a").ShouldBe(45.333, 0, 30, 20);
        h.Frame("c").ShouldBe(124.667, 0, 30, 20);
    }

    [Fact]
    public void SpreadInsideWithHiddenFirstSticksTheNextToTheStartThroughTheGoneMargin()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 10).Top(P);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 8, goneMargin: 2).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 16).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.SpreadInside }.With("a", "b", "c"));
        h.Fake("a").Visibility = Visibility.Collapsed;

        h.Layout(200, 100, 200, 100);

        // The first VISIBLE member takes the first-member role: pinned to the start (plus the gone margin),
        // the last sticks to the end.
        h.Frame("b").ShouldBe(2, 0, 30, 20);
        h.Frame("c").ShouldBe(170, 0, 30, 20);
    }

    [Fact]
    public void TwoHiddenMiddleMembersCollapseToTheGoneMarginOfTheFirstVisibleAnchor()
    {
        var h = new EngineHarness();
        h.View("a", 20, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 20, 20).Left("a", MagnetPole.Right, 5, goneMargin: 1).Top(P);
        h.View("c", 20, 20).Left("b", MagnetPole.Right, 6, goneMargin: 2).Top(P);
        h.View("d", 20, 20).Left("c", MagnetPole.Right, 7, goneMargin: 3).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b", "c", "d"));
        h.Fake("b").Visibility = Visibility.Collapsed;
        h.Fake("c").Visibility = Visibility.Collapsed;

        h.Layout(200, 100, 200, 100);

        // b and c drop their own margins entirely; the only surviving space is d's gone margin (3),
        // because d's anchor is the one pointing into the collapsed run.
        h.Frame("a").ShouldBe(0, 0, 20, 20);
        h.Frame("d").ShouldBe(23, 0, 20, 20);
    }

    [Fact]
    public void DefaultGoneMarginPreservesTheGapOfTheSurvivingAnchor()
    {
        // "10 A 20 B 30 C" with NO explicit gone margins: the gone margin defaults to the margin itself.
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 10).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 20).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 30).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b", "c"));

        // B gone → "10 A 30 C": C's own margin survives as its default gone margin.
        h.Fake("b").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(10, 0, 30, 20);
        h.Frame("c").ShouldBe(70, 0, 30, 20);

        // A also gone → "30 C" TODAY: the leading 10 belongs to A and collapses with it, C keeps its
        // default gone margin. Separator semantics ("10 C": first visible member adopts the chain's
        // leading margin) are not expressible statically — candidate for a chain-level opt-in.
        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("c").ShouldBe(30, 0, 30, 20);
    }

    [Fact]
    public void WeightedChainRedistributesTheShareOfAHiddenMember()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.View("b", 40, 20).Top(P).Size(40, 20);
        h.View("c", 30, 20).Right(P).Top(P).Size(MagnetSizing.Constraint, 20);
        var chain = h.Add(new MagnetChain { MagnetId = "row" }.With("a", "b", "c"));
        chain.Weights.Add(1);
        chain.Weights.Add(0);
        chain.Weights.Add(3);

        h.Fake("c").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);

        // c's weight (3) is excluded: a absorbs the whole remaining space (ConstraintLayout GONE semantics).
        h.Frame("a").ShouldBe(0, 0, 160, 20);
        h.Frame("b").ShouldBe(160, 0, 40, 20);
        h.Frame("c").ShouldBe(200, 0, 0, 0);

        // Showing it back restores the 1:3 split — visibility is handled at runtime, not by stale patches.
        h.Fake("c").Visibility = Visibility.Visible;
        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(0, 0, 40, 20);
        h.Frame("c").ShouldBe(80, 0, 120, 20);
    }

    [Fact]
    public void UniformChainGapAppliesBetweenMembers()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Top(P);
        h.View("c", 30, 20).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, Gap = 10 }.With("a", "b", "c"));

        h.Layout(200, 100, 200, 100);

        h.Frame("a").ShouldBe(0, 0, 30, 20);
        h.Frame("b").ShouldBe(40, 0, 30, 20);
        h.Frame("c").ShouldBe(80, 0, 30, 20);
    }

    [Fact]
    public void ChainGapHasSeparatorSemanticsWhenMembersCollapse()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Top(P);
        h.View("c", 30, 20).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, Gap = 10 }.With("a", "b", "c"));

        // Middle hidden: ONE gap between the two visible members — no gone margins to think about.
        h.Fake("b").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(0, 0, 30, 20);
        h.Frame("c").ShouldBe(40, 0, 30, 20);

        // Head run hidden: the first visible member gets NO leading gap.
        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("c").ShouldBe(0, 0, 30, 20);

        // Tail hidden: no trailing gap either.
        h.Fake("a").Visibility = Visibility.Visible;
        h.Fake("b").Visibility = Visibility.Visible;
        h.Fake("c").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(0, 0, 30, 20);
        h.Frame("b").ShouldBe(40, 0, 30, 20);
    }

    [Fact]
    public void PerPairAnchorOverridesTheChainGap()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 30).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, Gap = 10 }.With("a", "b", "c"));

        h.Layout(200, 100, 200, 100);
        h.Frame("b").ShouldBe(40, 0, 30, 20);
        h.Frame("c").ShouldBe(100, 0, 30, 20);

        // The anchored pair keeps the per-anchor gone semantics; the Gap pair keeps the separator semantics.
        h.Fake("b").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(0, 0, 30, 20);
        h.Frame("c").ShouldBe(60, 0, 30, 20);
    }

    [Fact]
    public void WeightedChainAccountsTheChainGap()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.View("b", 30, 20).Right(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.Add(new MagnetChain { MagnetId = "row", Gap = 20 }.With("a", "b"));

        h.Layout(200, 100, 200, 100);

        h.Frame("a").ShouldBe(0, 0, 90, 20);
        h.Frame("b").ShouldBe(110, 0, 90, 20);
    }

    [Fact]
    public void SpreadChainReservesTheGapAndSpreadsTheRest()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P);
        h.View("b", 30, 20).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Spread, Gap = 20 }.With("a", "b"));

        h.Layout(200, 100, 200, 100);

        // sizes 60 + gap 20 → slack 120 over 3 spread slots of 40.
        h.Frame("a").ShouldBe(40, 0, 30, 20);
        h.Frame("b").ShouldBe(130, 0, 30, 20);
    }

    [Fact]
    public void ZeroGapIsStructuralAndNonZeroTransitionsRecompile()
    {
        // The compiler omits the gap ops entirely for Gap == 0: the zero-ness is part of the fingerprint
        // and crossing 0 <-> non-zero is a Structure change (never a stale dead-input patch).
        MagnetNode[] Create(double gap) =>
        [
            new MagnetView().Id("a").Left(P).Top(P),
            new MagnetView().Id("b").Top(P),
            new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, Gap = gap }.With("a", "b")
        ];

        Nalu.MagnetLayout.Engine.MagnetCompiler.GetOrCompile(Create(0))
            .Should().NotBeSameAs(Nalu.MagnetLayout.Engine.MagnetCompiler.GetOrCompile(Create(10)));
        Nalu.MagnetLayout.Engine.MagnetCompiler.GetOrCompile(Create(4))
            .Should().BeSameAs(Nalu.MagnetLayout.Engine.MagnetCompiler.GetOrCompile(Create(8)), "non-zero gaps share the tape (patched value)");

        var chain = new MagnetChain { MagnetId = "row", Gap = 0 };
        var changes = new List<MagnetChange>();
        var probe = new OwnerProbe(changes);
        chain.Attach(probe);

        chain.Gap = 12;
        chain.Gap = 20;
        chain.Gap = 0;

        changes.Should().Equal(MagnetChange.Structure, MagnetChange.Values, MagnetChange.Structure);
    }

    private sealed class OwnerProbe(List<MagnetChange> changes) : IMagnetOwner
    {
        public void OnNodeChanged(MagnetNode? node, MagnetChange change) => changes.Add(change);

        public void OnApplyVisibilityRequested(MagnetView node) { }
    }

    [Fact]
    public void RuntimeGapFromZeroTakesEffectThroughRecompilation()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Top(P);
        var chain = h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b"));

        h.Layout(200, 100, 200, 100);
        h.Frame("b").ShouldBe(30, 0, 30, 20);

        // 0 -> 12 is structural: recompile (fresh tape) and the gap appears.
        chain.Gap = 12;
        h.Compile();
        h.Layout(200, 100, 200, 100);
        h.Frame("b").ShouldBe(42, 0, 30, 20);
    }

    [Fact]
    public void SingleWeightedMemberTakesTheWholeDistributableSpace()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.View("b", 40, 20).Right(P).Top(P).Size(40, 20);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed }.With("a", "b"));

        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(0, 0, 160, 20);

        // Hidden single weighted member: size zeroed by visibility regardless of the fraction shortcut
        // (the packed group — now just b — centers with the default bias, so the zero-size a sits at 80).
        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);
        h.Frame("a").ShouldBe(80, 0, 0, 0);
        h.Frame("b").ShouldBe(80, 0, 40, 20);
    }

    [Fact]
    public void GapIsAnAnimatableValuePatch()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Top(P);
        var chain = h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, Gap = 10 }.With("a", "b"));

        h.Layout(200, 100, 200, 100);
        h.Frame("b").ShouldBe(40, 0, 30, 20);
        var tape = h.Engine.Tape;

        chain.Gap = 30;
        h.Engine.PatchValues();
        h.Layout(200, 100, 200, 100);

        h.Engine.Tape.Should().BeSameAs(tape, "Gap is a patched value, not structure");
        h.Frame("b").ShouldBe(60, 0, 30, 20);
    }

    /// <summary>"10 A 20 B 30 C" in Separators mode: lead owned by the chain, inner margins gated by visibility.</summary>
    private static EngineHarness CreateSeparatorsRow()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 10).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 20).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 30).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, GapMode = MagnetChainGapMode.Separators }.With("a", "b", "c"));

        return h;
    }

    [Fact]
    public void SeparatorsModeMatchesAnchorsModeWhenEveryoneIsVisible()
    {
        var h = CreateSeparatorsRow();

        h.Layout(300, 100, 300, 100);

        h.Frame("a").ShouldBe(10, 0, 30, 20);
        h.Frame("b").ShouldBe(60, 0, 30, 20);
        h.Frame("c").ShouldBe(120, 0, 30, 20);
    }

    [Fact]
    public void SeparatorsModeKeepsTheSurvivorsSeparatorWhenTheMiddleCollapses()
    {
        var h = CreateSeparatorsRow();
        h.Fake("b").Visibility = Visibility.Collapsed;

        h.Layout(300, 100, 300, 100);

        // "10 A 30 C": C's own separator applies because a visible member precedes it.
        h.Frame("a").ShouldBe(10, 0, 30, 20);
        h.Frame("c").ShouldBe(70, 0, 30, 20);
    }

    [Fact]
    public void SeparatorsModePutsTheFirstVisibleMemberAtTheChainLead()
    {
        var h = CreateSeparatorsRow();
        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Fake("b").Visibility = Visibility.Collapsed;

        h.Layout(300, 100, 300, 100);

        // "10 C": the lead belongs to the chain (survives the head collapsing); C has no separator.
        h.Frame("c").ShouldBe(10, 0, 30, 20);

        // Showing everything back restores the full row.
        h.Fake("a").Visibility = Visibility.Visible;
        h.Fake("b").Visibility = Visibility.Visible;
        h.Layout(300, 100, 300, 100);
        h.Frame("c").ShouldBe(120, 0, 30, 20);
    }

    [Fact]
    public void SeparatorsModeDropsTheSeparatorOfAHiddenTail()
    {
        var h = CreateSeparatorsRow();
        h.Fake("c").Visibility = Visibility.Collapsed;

        h.Layout(300, 100, 300, 100);

        h.Frame("a").ShouldBe(10, 0, 30, 20);
        h.Frame("b").ShouldBe(60, 0, 30, 20);
    }

    [Fact]
    public void SeparatorsModeKeepsTheTrailingMarginWhenTheTailCollapses()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Bias(1, 0.5); // packed to the end
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 20).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 30).Right(P, margin: 12).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, GapMode = MagnetChainGapMode.Separators }.With("a", "b", "c"));

        h.Fake("c").Visibility = Visibility.Collapsed;
        h.Layout(300, 100, 300, 100);

        // The trailing 12 belongs to the chain: the last visible member ends at 300 - 12.
        h.Frame("b").Right.Should().Be(288);
    }

    [Fact]
    public void SeparatorsModeIgnoresGoneMarginsOnInnerPairs()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 10).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Left("a", MagnetPole.Right, 20, goneMargin: 2).Top(P);
        h.View("c", 30, 20).Left("b", MagnetPole.Right, 30, goneMargin: 4).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, GapMode = MagnetChainGapMode.Separators }.With("a", "b", "c"));

        h.Fake("b").Visibility = Visibility.Collapsed;
        h.Layout(300, 100, 300, 100);

        // The separator is C's raw margin (30), not its gone margin (4).
        h.Frame("c").ShouldBe(70, 0, 30, 20);
    }

    [Fact]
    public void SeparatorsModeComposesWithTheUniformChainGap()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P, margin: 10).Top(P).Bias(0, 0.5);
        h.View("b", 30, 20).Top(P);
        h.View("c", 30, 20).Top(P);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed, Gap = 8, GapMode = MagnetChainGapMode.Separators }.With("a", "b", "c"));

        h.Layout(300, 100, 300, 100);
        h.Frame("a").ShouldBe(10, 0, 30, 20);
        h.Frame("b").ShouldBe(48, 0, 30, 20);

        // Head hidden: the first visible member sits at the chain lead, not at lead + gap.
        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Layout(300, 100, 300, 100);
        h.Frame("b").ShouldBe(10, 0, 30, 20);
        h.Frame("c").ShouldBe(48, 0, 30, 20);
    }

    [Fact]
    public void GapModeIsPartOfTheStructuralFingerprint()
    {
        MagnetNode[] Create(MagnetChainGapMode mode) =>
        [
            new MagnetView().Id("a").Left(P, margin: 10).Top(P),
            new MagnetView().Id("b").Left("a", MagnetPole.Right, 20).Top(P),
            new MagnetChain { MagnetId = "row", GapMode = mode }.With("a", "b")
        ];

        Nalu.MagnetLayout.Engine.MagnetCompiler.GetOrCompile(Create(MagnetChainGapMode.Anchors))
            .Should().NotBeSameAs(Nalu.MagnetLayout.Engine.MagnetCompiler.GetOrCompile(Create(MagnetChainGapMode.Separators)));
    }

    [Fact]
    public void ChainMembersCenterVerticallyOnAGuidelineAndOnEachOther()
    {
        var h = new EngineHarness();
        h.Add(new MagnetGuideline { MagnetId = "mid", Orientation = MagnetOrientation.Horizontal, Position = 50 });
        h.View("a", 30, 40).Left(P).VerticallyWithin("mid");   // tall member centered ON the line
        h.View("b", 30, 16).VerticallyWithin("mid");            // short member centered ON the line
        h.View("c", 30, 24).VerticallyWithin("a");              // centered on another member's span
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Spread }.With("a", "b", "c"));

        h.Layout(200, 100, 200, 100);

        // VerticallyWithin a horizontal guideline spans a zero-height segment: bias 0.5 puts the CENTER on the line.
        h.Frame("a").Y.Should().Be(30);
        h.Frame("b").Y.Should().Be(42);
        (h.Frame("a").Y + (h.Frame("a").Height / 2)).Should().Be(50);
        (h.Frame("b").Y + (h.Frame("b").Height / 2)).Should().Be(50);
        (h.Frame("c").Y + (h.Frame("c").Height / 2)).Should().Be(50, "centering within a member aligns the centers even when the target is taller");
    }

    [Fact]
    public void AllWeightedMembersHiddenLeavesTheFixedMembersSpread()
    {
        var h = new EngineHarness();
        h.View("a", 30, 20).Left(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.View("b", 40, 20).Top(P).Size(40, 20);
        h.View("c", 30, 20).Right(P).Top(P).Size(MagnetSizing.Constraint, 20);
        h.Add(new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Spread }.With("a", "b", "c"));

        h.Fake("a").Visibility = Visibility.Collapsed;
        h.Fake("c").Visibility = Visibility.Collapsed;
        h.Layout(200, 100, 200, 100);

        // Total effective weight is 0 (no division blow-up): the fixed member spreads alone.
        h.Frame("b").ShouldBe(80, 0, 40, 20);
    }
}
