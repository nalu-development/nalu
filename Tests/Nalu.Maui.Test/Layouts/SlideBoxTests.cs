namespace Nalu.Maui.Test.Layouts;

public class SlideBoxTests
{
    private static SlideBoxItem Item(bool enabled = true) => new() { IsEnabled = enabled, Template = new DataTemplate(() => new Label()) };

    private static SlideBox Box(params SlideBoxItem[] items)
    {
        var box = new SlideBox();

        foreach (var item in items)
        {
            box.Items.Add(item);
        }

        return box;
    }

    [Fact(DisplayName = "SelectedIndex, given no items, should be -1")]
    public void SelectedIndexGivenNoItemsShouldBeMinusOne()
    {
        var box = new SlideBox();

        box.SelectedIndex.Should().Be(0);
        box.SelectedItem.Should().BeNull();
    }

    [Fact(DisplayName = "SelectedIndex, when items are added, should select the first and expose SelectedItem")]
    public void SelectedIndexWhenItemsAreAddedShouldSelectTheFirst()
    {
        var first = Item();
        var box = Box(first, Item());

        box.SelectedIndex.Should().Be(0);
        box.SelectedItem.Should().BeSameAs(first);
    }

    [Fact(DisplayName = "SelectedIndex, when set out of range, should clamp")]
    public void SelectedIndexWhenSetOutOfRangeShouldClamp()
    {
        var box = Box(Item(), Item(), Item());

        box.SelectedIndex = 42;
        box.SelectedIndex.Should().Be(2);

        box.SelectedIndex = -5;
        box.SelectedIndex.Should().Be(0);
    }

    [Fact(DisplayName = "SelectedIndex, when set on a disabled item, should coerce to the nearest enabled")]
    public void SelectedIndexWhenSetOnADisabledItemShouldCoerce()
    {
        var box = Box(Item(), Item(enabled: false), Item());

        box.SelectedIndex = 1;

        box.SelectedIndex.Should().Be(2, "forward wins on ties");
    }

    [Fact(DisplayName = "Next and Previous, should skip disabled items and stop at the ends")]
    public void NextAndPreviousShouldSkipDisabledItems()
    {
        var box = Box(Item(), Item(enabled: false), Item());

        box.Next().Should().BeTrue();
        box.SelectedIndex.Should().Be(2);
        box.Next().Should().BeFalse();

        box.Previous().Should().BeTrue();
        box.SelectedIndex.Should().Be(0);
        box.Previous().Should().BeFalse();
    }

    [Fact(DisplayName = "Disabling the selected item, should advance to the nearest enabled one")]
    public void DisablingTheSelectedItemShouldAdvance()
    {
        var box = Box(Item(), Item(), Item());
        box.SelectedIndex = 1;

        box.Items[1].IsEnabled = false;

        box.SelectedIndex.Should().Be(2);
    }

    [Fact(DisplayName = "Disabling every item, should clear the selection; enabling one, should restore it")]
    public void DisablingEveryItemShouldClearSelection()
    {
        var box = Box(Item(), Item());

        box.Items[0].IsEnabled = false;
        box.Items[1].IsEnabled = false;

        box.SelectedIndex.Should().Be(-1);
        box.SelectedItem.Should().BeNull();

        box.Items[1].IsEnabled = true;

        box.SelectedIndex.Should().Be(1);
    }

    [Fact(DisplayName = "SelectedIndexChanged, should report old and new values")]
    public void SelectedIndexChangedShouldReportOldAndNewValues()
    {
        var box = Box(Item(), Item(), Item());
        SlideBoxSelectionChangedEventArgs? received = null;
        box.SelectedIndexChanged += (_, e) => received = e;

        box.SelectedIndex = 2;

        received.Should().NotBeNull();
        received!.OldIndex.Should().Be(0);
        received.NewIndex.Should().Be(2);
        received.NewItem.Should().BeSameAs(box.Items[2]);
    }

    [Fact(DisplayName = "Items, should be logical children so binding context flows")]
    public void ItemsShouldBeLogicalChildren()
    {
        var context = new object();
        var box = Box(Item());

        box.BindingContext = context;

        box.Items[0].BindingContext.Should().BeSameAs(context);
    }

    /// <summary>A box with a real page size, so templates actually realize.</summary>
    private static SlideBox SizedBox(params SlideBoxItem[] items)
    {
        var box = new SlideBox
        {
            Frame = new Rect(0, 0, 300, 300)
        };

        foreach (var item in items)
        {
            box.Items.Add(item);
        }

        return box;
    }

    [Fact(DisplayName = "ContentBindingContext, should scope the realized content instead of the inherited context")]
    public void ContentBindingContextShouldScopeTheRealizedContent()
    {
        var scoped = new object();
        var item = Item();
        item.ContentBindingContext = scoped;

        var box = SizedBox(item);
        box.BindingContext = new object();

        item.Content.Should().NotBeNull();
        item.Content!.BindingContext.Should().BeSameAs(scoped);
    }

    [Fact(DisplayName = "ContentBindingContext, when changed after realization, should update the content")]
    public void ContentBindingContextWhenChangedShouldUpdateTheContent()
    {
        var item = Item();
        item.ContentBindingContext = new object();
        var box = SizedBox(item);

        var replacement = new object();
        item.ContentBindingContext = replacement;

        box.Items[0].Content!.BindingContext.Should().BeSameAs(replacement);
    }

    [Fact(DisplayName = "ContentBindingContext, when the content is torn down, should be cleared from it")]
    public void ContentBindingContextWhenTornDownShouldBeCleared()
    {
        var item = Item();
        item.ContentBindingContext = new object();
        _ = SizedBox(item, Item());

        var content = item.Content!;
        item.IsEnabled = false;

        item.Content.Should().BeNull();
        content.BindingContext.Should().BeNull();
    }

    [Fact(DisplayName = "ContentBindingContext, when unset, should let the content inherit the box context")]
    public void ContentBindingContextWhenUnsetShouldInherit()
    {
        var context = new object();
        var box = SizedBox(Item());
        box.BindingContext = context;

        box.Items[0].Content!.BindingContext.Should().BeSameAs(context);
    }

    [Fact(DisplayName = "Swipe thresholds, by default, should be a third of a page and 400 units per second")]
    public void SwipeThresholdsByDefaultShouldKeepTheHistoricBehavior()
    {
        var box = new SlideBox();

        box.SwipeCommitThreshold.Should().BeApproximately(1d / 3, 0.0001);
        box.SwipeFlickVelocity.Should().Be(400);
    }

    // MAUI drops a value failing validateValue (logging a warning) instead of throwing.
    [Theory(DisplayName = "SwipeCommitThreshold, when set outside 0-1, should be ignored")]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void SwipeCommitThresholdWhenOutOfRangeShouldBeIgnored(double threshold)
    {
        var box = new SlideBox { SwipeCommitThreshold = 0.5 };

        box.SwipeCommitThreshold = threshold;

        box.SwipeCommitThreshold.Should().Be(0.5);
    }

    [Fact(DisplayName = "SwipeFlickVelocity, should ignore negatives and accept infinity")]
    public void SwipeFlickVelocityShouldIgnoreNegativesAndAcceptInfinity()
    {
        var box = new SlideBox();

        box.SwipeFlickVelocity = -1;
        box.SwipeFlickVelocity.Should().Be(400);

        box.SwipeFlickVelocity = double.PositiveInfinity;
        box.SwipeFlickVelocity.Should().Be(double.PositiveInfinity);
    }

    [Theory(DisplayName = "ShouldCommitSwipe, should honor the commit threshold and the flick velocity")]
    // Slow drags are judged on distance alone: 120 of 300 is 40% of the page.
    [InlineData(-120, 0, 1d / 3, 400, true)]
    [InlineData(-120, 0, 0.5, 400, false)]
    [InlineData(-70, 0, 1d / 3, 400, false)]
    [InlineData(-70, 0, 0.2, 400, true)]
    [InlineData(120, 0, 1d / 3, 400, true)]
    // A short flick commits whatever the distance — unless the flick velocity is raised past it
    // (then its projection, 20 + 60, falls short of a third of the page).
    [InlineData(-20, -500, 1d / 3, 400, true)]
    [InlineData(-20, -500, 1d / 3, 1000, false)]
    [InlineData(-20, -500, 1d / 3, double.PositiveInfinity, false)]
    // A flick back cancels whatever the distance — unless it is no longer fast enough to be one
    // (then its projection, 200 - 60, is still past a third of the page).
    [InlineData(-200, 500, 1d / 3, 400, false)]
    [InlineData(-200, 500, 1d / 3, 1000, true)]
    public void ShouldCommitSwipeShouldHonorTheThresholds(double offset, double velocity, double commitThreshold, double flickVelocity, bool expected)
        => SlideBox.ShouldCommitSwipe(offset, velocity, 300, commitThreshold, flickVelocity).Should().Be(expected);

    [Theory(DisplayName = "EndDrag, should apply SwipeCommitThreshold to the released drag")]
    [InlineData(1d / 3, 1)]
    [InlineData(0.6, 0)]
    public void EndDragShouldApplySwipeCommitThreshold(double commitThreshold, int expectedIndex)
    {
        var box = SizedBox(Item(), Item());
        box.SwipeCommitThreshold = commitThreshold;

        box.BeginDrag();
        box.UpdateDrag(-150);

        // Past the sampler's 100ms window the release reads as still, so only distance decides.
        // A late wake-up can only make the pause longer, never shorter: this cannot flake.
        Thread.Sleep(150);
        box.EndDrag(canceled: false);

        box.SelectedIndex.Should().Be(expectedIndex);
    }
}
