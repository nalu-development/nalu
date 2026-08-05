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
}
