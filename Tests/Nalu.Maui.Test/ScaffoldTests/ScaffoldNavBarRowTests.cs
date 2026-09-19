using Microsoft.Maui.Layouts;
using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The default bar's row layout, driven headlessly with children of known size: the fixed parts
/// (leading, trailing) are measured first, the toolbar strip is offered the row minus those and
/// the title's reservation, the title takes what the strip leaves — and the arrange mirrors the
/// measure, left-to-right and right-to-left, never producing a negative width.
/// </summary>
public class ScaffoldNavBarRowTests
{
    public ScaffoldNavBarRowTests() => DispatcherProvider.SetCurrent(new DispatcherProviderStub());

    /// <summary>
    /// A child of known natural size that honors its width constraint, with an optional minimum
    /// (the strip never measures below its overflow button), and reports its height regardless
    /// of the constraint — like the 44dp chrome, which carries a HeightRequest. Records every
    /// width constraint it is measured with, in order.
    /// </summary>
    private sealed class Probe : View
    {
        public double NaturalWidth { get; init; }
        public double NaturalHeight { get; init; } = 44;
        public double MinimumWidth { get; init; }
        public List<double> Constraints { get; } = [];

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            Constraints.Add(widthConstraint);

            var width = Math.Max(MinimumWidth, Math.Min(NaturalWidth, widthConstraint));

            return new Size(width, NaturalHeight);
        }
    }

    /// <summary>The default rig: a 44dp back button, a 100dp title, a 150dp strip, no trailing buttons.</summary>
    private sealed class Rig
    {
        public Probe Leading { get; }
        public Probe Title { get; }
        public Probe Strip { get; }
        public Probe Trailing { get; }
        public ScaffoldNavBarRow Row { get; }
        public ILayoutManager Manager { get; }

        public Rig(
            double leading = 44,
            double title = 100,
            double strip = 150,
            double trailing = 0,
            double stripMinimum = 44,
            double titleHeight = 44,
            double stripHeight = 44,
            double spacing = 8,
            double minimumTitleWidth = 96,
            bool keepTitleWhole = true,
            Thickness? padding = null)
        {
            Leading = new Probe { NaturalWidth = leading };
            Title = new Probe { NaturalWidth = title, NaturalHeight = titleHeight };
            Strip = new Probe { NaturalWidth = strip, MinimumWidth = stripMinimum, NaturalHeight = stripHeight };
            Trailing = new Probe { NaturalWidth = trailing };

            Row = new ScaffoldNavBarRow(Leading, Title, Strip, Trailing)
            {
                Spacing = spacing,
                MinimumTitleWidth = minimumTitleWidth,
                KeepTitleWhole = keepTitleWhole,
                Padding = padding ?? new Thickness(8, 0)
            };

            Manager = Row.CreateManager();
        }

        public Size Measure(double width = 400, double height = 48) => Manager.Measure(width, height);

        public Size Arrange(double width = 400, double height = 48, double x = 0, double y = 0)
        {
            Measure(width, height);

            return Manager.ArrangeChildren(new Rect(x, y, width, height));
        }
    }

    #region Measure — the offer to each part

    [Fact(DisplayName = "A bounded row fills its width and is as tall as its tallest part")]
    public void BoundedRowFillsTheWidth()
    {
        var rig = new Rig();

        rig.Measure(400, 48).Should().Be(new Size(400, 44), "the row spans the bar; its height is the children's, not the constraint");
    }

    [Fact(DisplayName = "Leading and trailing parts are measured unbounded — they are never squeezed")]
    public void FixedPartsAreMeasuredUnbounded()
    {
        var rig = new Rig(trailing: 88);
        rig.Measure();

        rig.Leading.Constraints.Should().Equal(double.PositiveInfinity);
        rig.Trailing.Constraints.Should().Equal(double.PositiveInfinity);
    }

    [Fact(DisplayName = "The strip is offered the row minus padding, fixed parts, both spacings and the title's reservation")]
    public void StripIsOfferedWhatIsLeftAfterTheTitleReservation()
    {
        var rig = new Rig();
        rig.Measure(400);

        // 400 - 16 padding = 384; - 44 leading - 0 trailing - 2×8 spacing = 324; - 100 title = 224.
        rig.Strip.Constraints.Should().Equal(224);
    }

    [Fact(DisplayName = "The title is measured natural first, then at what the strip leaves")]
    public void TitleIsMeasuredNaturalThenFinal()
    {
        var rig = new Rig();
        rig.Measure(400);

        // 384 - 60 fixed - 150 strip = 174: wider than its 100 natural — no truncation.
        rig.Title.Constraints.Should().Equal(double.PositiveInfinity, 174);
    }

    [Fact(DisplayName = "A title longer than the floor reserves its natural width: the strip shrinks")]
    public void LongTitleReservesItsNaturalWidth()
    {
        var rig = new Rig(title: 250);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal(384 - 60 - 250);
    }

    [Fact(DisplayName = "A title shorter than the floor reserves the floor")]
    public void ShortTitleReservesTheFloor()
    {
        var rig = new Rig(title: 40, minimumTitleWidth: 96);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal(384 - 60 - 96);
    }

    [Fact(DisplayName = "With KeepTitleWhole off only the floor is reserved, even for a long title")]
    public void KeepTitleWholeOffReservesTheFloorOnly()
    {
        var rig = new Rig(title: 250, keepTitleWhole: false, minimumTitleWidth: 96);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal(384 - 60 - 96);
        rig.Title.Constraints.Last().Should().Be(384 - 60 - 150, "the title then truncates to what the strip leaves");
    }

    [Fact(DisplayName = "A zero floor with KeepTitleWhole off gives the strip everything beside the fixed parts")]
    public void ZeroFloorItemsWinOutright()
    {
        var rig = new Rig(title: 250, keepTitleWhole: false, minimumTitleWidth: 0);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal(384 - 60);
    }

    [Fact(DisplayName = "The reservation never exceeds what the row has beside the fixed parts")]
    public void ReservationIsCappedToTheRow()
    {
        var rig = new Rig(title: 500);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal([0.0], "a title wider than the row leaves the strip nothing");
    }

    [Fact(DisplayName = "The overflow button is the only thing that can cost the title: a strip at its minimum truncates a too-long title")]
    public void OverflowSlotIsTheOnlyCostToTheTitle()
    {
        var rig = new Rig(title: 500, stripMinimum: 44);
        rig.Measure(400);

        rig.Strip.NaturalWidth.Should().Be(150);
        rig.Title.Constraints.Last().Should().Be(384 - 60 - 44, "offered nothing, the strip still holds its 44dp overflow slot; the title gets the rest");
    }

    [Fact(DisplayName = "With no toolbar items the title gets the whole remainder")]
    public void NoItemsGiveTheTitleEverything()
    {
        var rig = new Rig(strip: 0, stripMinimum: 0, title: 500);
        rig.Measure(400);

        rig.Title.Constraints.Last().Should().Be(384 - 60, "an empty strip measures zero and costs nothing");
    }

    [Fact(DisplayName = "A strip that ignores its budget is paid for by the title, never by the fixed parts")]
    public void OverwideStripCostsTheTitleOnly()
    {
        // The probe caps at its constraint, so a budget-ignoring strip is modeled with a minimum.
        var rig = new Rig(stripMinimum: 300);
        rig.Measure(400);

        rig.Title.Constraints.Last().Should().Be(384 - 60 - 300);
        rig.Measure(400).Should().Be(new Size(400, 44), "the row still reports its own width");
    }

    #endregion

    #region Measure — edge sizes

    [Fact(DisplayName = "A row narrower than its fixed parts never offers a negative width")]
    public void NarrowRowNeverGoesNegative()
    {
        var rig = new Rig();
        var size = rig.Measure(50);

        rig.Strip.Constraints.Should().Equal(0);
        rig.Title.Constraints.Last().Should().Be(0);
        size.Should().Be(new Size(50, 44));
    }

    [Fact(DisplayName = "A zero-width row measures without throwing and offers nothing")]
    public void ZeroWidthRowIsSafe()
    {
        var rig = new Rig();
        var act = () => rig.Measure(0);

        act.Should().NotThrow();
        rig.Strip.Constraints.Should().Equal(0);
        rig.Title.Constraints.Last().Should().Be(0);
    }

    [Fact(DisplayName = "An unbounded row measures to the natural sum of its parts")]
    public void UnboundedRowMeasuresNaturalSum()
    {
        var rig = new Rig();
        var size = rig.Measure(double.PositiveInfinity);

        rig.Strip.Constraints.Should().Equal([double.PositiveInfinity], "unbounded, nothing needs folding");
        rig.Title.Constraints.Should().Equal([double.PositiveInfinity], "no reservation to compute: the title is measured once, unbounded");

        // 44 leading + 0 trailing + 16 spacing + 150 strip + 100 title + 16 padding.
        size.Should().Be(new Size(326, 44));
    }

    [Fact(DisplayName = "An unbounded height is passed through and the result is the tallest part")]
    public void UnboundedHeightIsPassedThrough()
    {
        var rig = new Rig(stripHeight: 56);
        var size = rig.Measure(400, double.PositiveInfinity);

        size.Height.Should().Be(56);
    }

    [Fact(DisplayName = "Height is the tallest part plus the vertical padding")]
    public void HeightIsTheTallestPartPlusPadding()
    {
        new Rig(titleHeight: 20).Measure().Height.Should().Be(44, "the buttons are taller than the title");
        new Rig(stripHeight: 56).Measure().Height.Should().Be(56, "a tall strip wins");
        new Rig(padding: new Thickness(8, 5, 8, 7)).Measure().Height.Should().Be(44 + 12);
    }

    [Fact(DisplayName = "Padding is taken off the offer and added back to the result")]
    public void PaddingIsSubtractedThenAdded()
    {
        var rig = new Rig(padding: new Thickness(10, 5, 20, 7));
        var size = rig.Measure(400, 48);

        // 400 - 30 = 370 inner; - 60 fixed - 100 title = 210 for the strip.
        rig.Strip.Constraints.Should().Equal(210);
        rig.Leading.Constraints.Should().Equal(double.PositiveInfinity);
        size.Should().Be(new Size(400, 44 + 12));
    }

    [Fact(DisplayName = "Padding larger than the row clamps the offer at zero")]
    public void OversizedPaddingClampsAtZero()
    {
        var rig = new Rig(padding: new Thickness(300, 0));
        var act = () => rig.Measure(400);

        act.Should().NotThrow();
        rig.Strip.Constraints.Should().Equal(0);
    }

    [Fact(DisplayName = "Spacing surrounds the title on both sides even when the buttons beside it are absent")]
    public void SpacingSurroundsTheTitleAlways()
    {
        var rig = new Rig(leading: 0, trailing: 0, spacing: 8);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal([384.0 - 16 - 100], "the two 8dp gaps stay so the title keeps its rhythm with or without buttons");
    }

    [Fact(DisplayName = "Zero spacing removes both gaps")]
    public void ZeroSpacingRemovesTheGaps()
    {
        var rig = new Rig(spacing: 0);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal(384 - 44 - 100);
    }

    [Fact(DisplayName = "Trailing buttons reduce the offer like leading ones")]
    public void TrailingButtonsReduceTheOffer()
    {
        var rig = new Rig(trailing: 88);
        rig.Measure(400);

        rig.Strip.Constraints.Should().Equal(384 - 44 - 88 - 16 - 100);
    }

    #endregion

    #region Measure — stability and settings

    [Fact(DisplayName = "Measuring again with the same inputs gives the same answer and the same offers")]
    public void MeasureIsIdempotent()
    {
        var rig = new Rig();
        var first = rig.Measure();
        var second = rig.Measure();

        second.Should().Be(first);
        rig.Strip.Constraints.Should().Equal(224, 224);
        rig.Title.Constraints.Should().Equal(double.PositiveInfinity, 174, double.PositiveInfinity, 174);
    }

    [Fact(DisplayName = "Changing spacing, the floor or KeepTitleWhole changes the next offer")]
    public void SettingsReflowOnTheNextMeasure()
    {
        var rig = new Rig(title: 100);
        rig.Measure();
        rig.Strip.Constraints.Last().Should().Be(224);

        rig.Row.Spacing = 0;
        rig.Measure();
        rig.Strip.Constraints.Last().Should().Be(240, "no gaps");

        rig.Row.MinimumTitleWidth = 200;
        rig.Measure();
        rig.Strip.Constraints.Last().Should().Be(140, "a floor above the natural width wins");

        rig.Row.KeepTitleWhole = false;
        rig.Measure();
        rig.Strip.Constraints.Last().Should().Be(140, "off, the floor alone is reserved — still 200 here");

        rig.Row.MinimumTitleWidth = 0;
        rig.Measure();
        rig.Strip.Constraints.Last().Should().Be(340, "off and no floor: the strip gets everything beside the fixed parts");
    }

    [Fact(DisplayName = "Setters invalidate the measure only when the value actually changes")]
    public void SettersInvalidateOnlyOnChange()
    {
        var rig = new Rig();
        var invalidations = 0;
        rig.Row.MeasureInvalidated += (_, _) => invalidations++;

        rig.Row.Spacing = 8;
        rig.Row.MinimumTitleWidth = 96;
        rig.Row.KeepTitleWhole = true;
        invalidations.Should().Be(0, "same values, nothing to redo");

        rig.Row.Spacing = 4;
        rig.Row.MinimumTitleWidth = 120;
        rig.Row.KeepTitleWhole = false;
        invalidations.Should().Be(3);
    }

    #endregion

    #region Arrange

    [Fact(DisplayName = "Left to right: leading | gap | title | gap | strip | trailing, the title absorbing the slack")]
    public void ArrangesLeftToRight()
    {
        var rig = new Rig();
        rig.Arrange(400, 48).Should().Be(new Size(400, 48), "arrange reports the bounds it was given");

        rig.Leading.Frame.Should().Be(new Rect(8, 0, 44, 48));
        rig.Title.Frame.Should().Be(new Rect(60, 0, 174, 48), "everything the fixed parts and the strip don't take");
        rig.Strip.Frame.Should().Be(new Rect(242, 0, 150, 48));
        rig.Trailing.Frame.Should().Be(new Rect(392, 0, 0, 48));
    }

    [Fact(DisplayName = "Right to left mirrors every slot")]
    public void ArrangesRightToLeft()
    {
        var rig = new Rig();
        rig.Row.FlowDirection = FlowDirection.RightToLeft;
        rig.Arrange(400, 48);

        rig.Leading.Frame.Should().Be(new Rect(348, 0, 44, 48), "the leading buttons sit at the right edge");
        rig.Title.Frame.Should().Be(new Rect(166, 0, 174, 48));
        rig.Strip.Frame.Should().Be(new Rect(8, 0, 150, 48));
        rig.Trailing.Frame.Should().Be(new Rect(8, 0, 0, 48), "the trailing buttons sit at the left edge");
    }

    [Fact(DisplayName = "Parts never overlap when everything fits")]
    public void PartsNeverOverlap()
    {
        var rig = new Rig(trailing: 88);
        rig.Arrange(400, 48);

        rig.Title.Frame.X.Should().BeGreaterThanOrEqualTo(rig.Leading.Frame.Right + 8);
        rig.Strip.Frame.X.Should().BeGreaterThanOrEqualTo(rig.Title.Frame.Right + 8);
        rig.Trailing.Frame.X.Should().BeGreaterThanOrEqualTo(rig.Strip.Frame.Right);
        rig.Trailing.Frame.Right.Should().Be(392, "flush with the inner right edge");
    }

    [Fact(DisplayName = "A wider row goes entirely to the title")]
    public void SlackGoesToTheTitle()
    {
        var rig = new Rig();
        rig.Arrange(600, 48);

        rig.Title.Frame.Width.Should().Be(584 - 44 - 150 - 16);
        rig.Strip.Frame.Should().Be(new Rect(584 + 8 - 150, 0, 150, 48), "the strip stays at its measured width, flush with the trailing edge");
    }

    [Fact(DisplayName = "The title width clamps at zero when the strip's minimum overflows a narrow row")]
    public void TitleWidthClampsAtZero()
    {
        var rig = new Rig();
        rig.Arrange(50, 48);

        rig.Title.Frame.Width.Should().Be(0);
        rig.Title.Frame.Height.Should().Be(48);
        rig.Strip.Frame.Width.Should().Be(44, "the overflow slot is kept even when it cannot fit");
        rig.Leading.Frame.Should().Be(new Rect(8, 0, 44, 48), "the leading buttons never move");
    }

    [Fact(DisplayName = "The bounds origin and the padding offset every part")]
    public void OriginAndPaddingOffsetTheParts()
    {
        var rig = new Rig(padding: new Thickness(10, 5, 20, 7));
        rig.Arrange(400, 48, x: 100, y: 30);

        rig.Leading.Frame.Should().Be(new Rect(110, 35, 44, 36));
        rig.Trailing.Frame.Right.Should().Be(100 + 400 - 20, "flush with the padded right edge");
        rig.Trailing.Frame.Height.Should().Be(48 - 12);
    }

    [Fact(DisplayName = "A vertically centered part is centered in the row band")]
    public void VerticallyCenteredPartIsCentered()
    {
        var rig = new Rig(titleHeight: 20);
        rig.Title.VerticalOptions = LayoutOptions.Center;
        rig.Arrange(400, 48);

        rig.Title.Frame.Y.Should().Be(14, "(48 - 20) / 2");
        rig.Title.Frame.Height.Should().Be(20);
        rig.Leading.Frame.Height.Should().Be(48, "a filling part spans the band");
    }

    [Fact(DisplayName = "Right to left with a narrow row still keeps the leading buttons on their edge")]
    public void NarrowRightToLeftKeepsTheEdges()
    {
        var rig = new Rig();
        rig.Row.FlowDirection = FlowDirection.RightToLeft;
        rig.Arrange(50, 48);

        rig.Leading.Frame.Should().Be(new Rect(50 - 8 - 44, 0, 44, 48));
        rig.Title.Frame.Width.Should().Be(0);
    }

    [Fact(DisplayName = "Arranging after a re-measure at a new width reflows without stale frames")]
    public void ArrangeFollowsTheLatestMeasure()
    {
        var rig = new Rig();
        rig.Arrange(600, 48);
        rig.Arrange(400, 48);

        rig.Title.Frame.Should().Be(new Rect(60, 0, 174, 48));
        rig.Strip.Frame.Should().Be(new Rect(242, 0, 150, 48));
    }

    #endregion
}
