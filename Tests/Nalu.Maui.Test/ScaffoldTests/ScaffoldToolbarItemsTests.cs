using System.Windows.Input;
using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The page's <see cref="Page.ToolbarItems"/> through the nav bar: the context exposes the live
/// collection, the strip renders primary items as buttons by priority and the overflow button
/// only while secondary items exist, a button follows its item and activates it the native
/// way, and font icons take the bar foreground.
/// </summary>
public class ScaffoldToolbarItemsTests
{
    public ScaffoldToolbarItemsTests() => DispatcherProvider.SetCurrent(new DispatcherProviderStub());

    private static (Scaffold Scaffold, ScaffoldRoot Root) BuildScaffold()
    {
        var root = new ScaffoldRoot();
        var area = new ScaffoldArea();
        area.Roots.Add(root);
        var scaffold = new Scaffold();
        scaffold.Areas.Add(area);

        return (scaffold, root);
    }

    /// <summary>Hosts the page as the root's page and returns its nav bar context.</summary>
    private static ScaffoldNavBarContext Host(Page page)
    {
        var (scaffold, root) = BuildScaffold();
        root.NavigationStack.RootPage = page;

        return scaffold.GetPageHost(page)!.Context;
    }

    private static ToolbarItem Item(string text, int priority = 0, ToolbarItemOrder order = ToolbarItemOrder.Default)
        => new() { Text = text, Priority = priority, Order = order };

    private static string[] Texts(ScaffoldToolbarItemsView strip)
        => strip.Buttons.Select(button => button.Item!.Text).ToArray();

    private static Label LabelOf(ScaffoldToolbarItemButton button) => ((Grid)button.Content!).Children.OfType<Label>().Single();

    private static Image IconOf(ScaffoldToolbarItemButton button) => ((Grid)button.Content!).Children.OfType<Image>().Single();

    [Fact(DisplayName = "The context exposes the page's own live collection and releases it once the page is gone")]
    public void ContextExposesThePageItems()
    {
        var page = new ContentPage();
        page.ToolbarItems.Add(Item("A"));
        var (scaffold, root) = BuildScaffold();
        root.NavigationStack.RootPage = page;
        var context = scaffold.GetPageHost(page)!.Context;

        context.ToolbarItems.Should().BeSameAs(page.ToolbarItems, "the page's collection IS the observable surface — no snapshot");

        root.NavigationStack.RootPage = null;
        scaffold.FlushRetiredPages();

        context.ToolbarItems.Should().BeNull("the items are parented to the page: holding them would keep the dead page alive");
    }

    [Fact(DisplayName = "Primary items become buttons by ascending priority; the overflow button shows only for secondary ones")]
    public void StripRendersPrimaryByPriorityAndOverflowForSecondary()
    {
        var page = new ContentPage();
        page.ToolbarItems.Add(Item("A", priority: 2));
        page.ToolbarItems.Add(Item("B", priority: 0, order: ToolbarItemOrder.Primary));
        page.ToolbarItems.Add(Item("C", order: ToolbarItemOrder.Secondary));
        page.ToolbarItems.Add(Item("D", priority: 1));

        var strip = new ScaffoldToolbarItemsView { BindingContext = Host(page) };

        Texts(strip).Should().Equal("B", "D", "A");
        strip.HasSecondaryItems.Should().BeTrue("C is secondary");
        strip.Children.Last().Should().BeSameAs(strip.OverflowButton, "the overflow button stays last");
    }

    [Fact(DisplayName = "Equal priorities keep declaration order")]
    public void EqualPrioritiesKeepDeclarationOrder()
    {
        var page = new ContentPage();
        page.ToolbarItems.Add(Item("First"));
        page.ToolbarItems.Add(Item("Second"));
        page.ToolbarItems.Add(Item("Third"));

        var strip = new ScaffoldToolbarItemsView { BindingContext = Host(page) };

        Texts(strip).Should().Equal("First", "Second", "Third");
    }

    [Fact(DisplayName = "The strip follows the collection and the items' Order/Priority live")]
    public void StripFollowsRuntimeMutations()
    {
        var page = new ContentPage();
        var a = Item("A", priority: 1);
        var c = Item("C", order: ToolbarItemOrder.Secondary);
        page.ToolbarItems.Add(a);
        page.ToolbarItems.Add(Item("B", priority: 0));
        page.ToolbarItems.Add(c);

        var strip = new ScaffoldToolbarItemsView { BindingContext = Host(page) };
        Texts(strip).Should().Equal("B", "A");

        page.ToolbarItems.Remove(c);
        strip.HasSecondaryItems.Should().BeFalse("no secondary item is left");

        page.ToolbarItems.Add(Item("E", priority: 5));
        Texts(strip).Should().Equal("B", "A", "E");

        a.Order = ToolbarItemOrder.Secondary;
        Texts(strip).Should().Equal(["B", "E"], "an item moved to the secondary surface leaves the strip");
        strip.HasSecondaryItems.Should().BeTrue();

        a.Order = ToolbarItemOrder.Primary;
        a.Priority = 10;
        Texts(strip).Should().Equal(["B", "E", "A"], "a priority change re-sorts");

        page.ToolbarItems.Clear();
        strip.Buttons.Should().BeEmpty();
        strip.HasSecondaryItems.Should().BeFalse();
    }

    [Fact(DisplayName = "A strip re-pointed at another page's context shows that page's items")]
    public void StripFollowsAContextSwap()
    {
        var first = new ContentPage();
        first.ToolbarItems.Add(Item("One"));
        var second = new ContentPage();
        second.ToolbarItems.Add(Item("Two"));

        var strip = new ScaffoldToolbarItemsView { BindingContext = Host(first) };
        Texts(strip).Should().Equal("One");

        strip.BindingContext = Host(second);
        Texts(strip).Should().Equal("Two");

        // The old page's collection must be released: mutating it no longer reaches the strip.
        first.ToolbarItems.Add(Item("Stale"));
        Texts(strip).Should().Equal("Two");
    }

    [Fact(DisplayName = "A button shows the icon when set, the text otherwise, and follows the item")]
    public void ButtonRendersIconOrTextAndFollowsTheItem()
    {
        var item = Item("Save");
        var button = new ScaffoldToolbarItemButton { Item = item };

        LabelOf(button).IsVisible.Should().BeTrue();
        LabelOf(button).Text.Should().Be("Save");
        IconOf(button).IsVisible.Should().BeFalse();
        SemanticProperties.GetDescription(button).Should().Be("Save", "the text is the accessible name");

        item.IconImageSource = "share.png";
        IconOf(button).IsVisible.Should().BeTrue();
        IconOf(button).Source.Should().BeSameAs(item.IconImageSource, "a bitmap renders as given");
        LabelOf(button).IsVisible.Should().BeFalse("an icon replaces the text, which stays the accessible name");
        SemanticProperties.GetDescription(button).Should().Be("Save");

        item.Text = "Store";
        SemanticProperties.GetDescription(button).Should().Be("Store");
        LabelOf(button).Text.Should().Be("Store");

        item.IsEnabled = false;
        button.IsEnabled.Should().BeFalse();
        button.Opacity.Should().BeApproximately(0.38, 0.001, "a disabled item is dimmed");

        item.IsEnabled = true;
        button.Opacity.Should().Be(1);
    }

    [Fact(DisplayName = "The item's AutomationId identifies the button")]
    public void ButtonTakesTheItemAutomationId()
    {
        var item = Item("Save");
        item.AutomationId = "SaveItem";

        new ScaffoldToolbarItemButton { Item = item }.AutomationId.Should().Be("SaveItem");
    }

    [Fact(DisplayName = "Activation runs the command and Clicked, and a disabled item is inert")]
    public void ActivationIsNativeAndGated()
    {
        var executed = new List<object?>();
        var clicked = 0;

        var item = new ToolbarItem
        {
            Text = "Save",
            Command = new Command<object?>(parameter => executed.Add(parameter)),
            CommandParameter = 42
        };

        item.Clicked += (_, _) => clicked++;
        var button = new ScaffoldToolbarItemButton { Item = item };

        button.Activate();
        executed.Should().Equal(42);
        clicked.Should().Be(1);

        item.IsEnabled = false;
        button.Activate();
        executed.Should().Equal([42], "a disabled item runs nothing");
        clicked.Should().Be(1, "not even Clicked — MAUI's own Activate would fire it, but a native disabled item is never tappable");

        // A command that cannot execute disables the item too (MAUI's IsEnabled coercion).
        item.IsEnabled = true;
        var gate = new GatedCommand();
        item.Command = gate;
        button.IsEnabled.Should().BeFalse();
        gate.Open();
        button.IsEnabled.Should().BeTrue();
    }

    [Fact(DisplayName = "A font icon declared without a color takes the bar foreground; anything else renders as given")]
    public void FontIconWithoutColorTakesTheForeground()
    {
        var page = new ContentPage();
        var item = new ToolbarItem { Text = "Share", IconImageSource = new FontImageSource { Glyph = "s", FontFamily = "Icons", Size = 20 } };
        page.ToolbarItems.Add(item);
        var context = Host(page);
        var strip = new ScaffoldToolbarItemsView { BindingContext = context };
        var button = strip.Buttons.Single();

        var tinted = IconOf(button).Source.Should().BeOfType<FontImageSource>().Subject;
        tinted.Should().NotBeSameAs(item.IconImageSource, "the tint is a derived source — the page's own icon is never mutated");
        tinted.Color.Should().Be(ScaffoldNavBarDefaults.Foreground);
        tinted.Glyph.Should().Be("s");
        tinted.FontFamily.Should().Be("Icons");
        tinted.Size.Should().Be(20);

        context.Foreground = Colors.Red;
        IconOf(button).Source.Should().BeOfType<FontImageSource>().Which.Color.Should().Be(Colors.Red, "the tint follows the appearance chain");

        button.TextColor = Colors.Green;
        IconOf(button).Source.Should().BeOfType<FontImageSource>().Which.Color.Should().Be(Colors.Green, "an explicit color on the primitive wins");

        var colored = new FontImageSource { Glyph = "s", FontFamily = "Icons", Color = Colors.Blue };
        item.IconImageSource = colored;
        IconOf(button).Source.Should().BeSameAs(colored, "a colored font icon renders exactly as given");
    }

    [Fact(DisplayName = "Button text follows the foreground chain unless styled")]
    public void ButtonTextColorFollowsTheChain()
    {
        var page = new ContentPage();
        page.ToolbarItems.Add(Item("Save"));
        var context = Host(page);
        var strip = new ScaffoldToolbarItemsView { BindingContext = context };
        var button = strip.Buttons.Single();

        LabelOf(button).TextColor.Should().Be(ScaffoldNavBarDefaults.Foreground);

        context.Foreground = Colors.Red;
        LabelOf(button).TextColor.Should().Be(Colors.Red);

        button.TextColor = Colors.Green;
        LabelOf(button).TextColor.Should().Be(Colors.Green, "an explicit color wins over the chain");
    }

    [Fact(DisplayName = "The strip fits items in priority order and folds the rest behind the overflow button")]
    public void FitFoldsWhatDoesNotFitInOrder()
    {
        double[] widths = [59, 44, 74, 62, 62];

        ScaffoldToolbarItemsView.Fit(widths, 44, hasSecondary: false, available: 400)
                                .Should().Be((5, false, 301.0), "everything fits and nothing is secondary: no overflow button");

        ScaffoldToolbarItemsView.Fit(widths, 44, hasSecondary: true, available: 400)
                                .Should().Be((5, true, 345.0), "secondary items keep the overflow button even when every primary item fits");

        ScaffoldToolbarItemsView.Fit(widths, 44, hasSecondary: false, available: 200)
                                .Should().Be((2, true, 147.0), "59 + 44 fit in the 156 left beside the overflow button; the 74 doesn't, and stops the intake");

        ScaffoldToolbarItemsView.Fit(widths, 44, hasSecondary: false, available: 30)
                                .Should().Be((0, true, 44.0), "nothing fits: only the overflow button, wider than the offer if need be");

        ScaffoldToolbarItemsView.Fit(widths, 44, hasSecondary: false, available: double.PositiveInfinity)
                                .Should().Be((5, false, 301.0), "unbounded, everything fits");

        ScaffoldToolbarItemsView.Fit([50, 50], 44, hasSecondary: false, available: 100)
                                .Should().Be((2, false, 100.0), "an exact fit is a fit");

        ScaffoldToolbarItemsView.Fit([], 44, hasSecondary: true, available: 100)
                                .Should().Be((0, true, 44.0), "secondary items alone still need the button");
    }

    [Fact(DisplayName = "The title is kept whole above a 96dp floor by default; both are plain bar properties")]
    public void TitleReservationDefaultsAreCustomizable()
    {
        var bar = new ScaffoldNavBarView();

        bar.KeepTitleWhole.Should().BeTrue();
        bar.MinimumTitleWidth.Should().Be(96);

        bar.KeepTitleWhole = false;
        bar.MinimumTitleWidth = 120;
        bar.KeepTitleWhole.Should().BeFalse();
        bar.MinimumTitleWidth.Should().Be(120);
    }

    [Fact(DisplayName = "The title reserves its natural width (items fold, the title never truncates), floored and capped")]
    public void TitleReservationIsNaturalWidthAboveTheFloor()
    {
        ScaffoldNavBarRow.ComputeTitleReservation(minimum: 200, natural: 110, keepWhole: true, available: 320)
                         .Should().Be(200, "a short title still keeps the floor");

        ScaffoldNavBarRow.ComputeTitleReservation(minimum: 200, natural: 260, keepWhole: true, available: 320)
                         .Should().Be(260, "a long title reserves its natural width: items fold before it truncates");

        ScaffoldNavBarRow.ComputeTitleReservation(minimum: 200, natural: 260, keepWhole: false, available: 320)
                         .Should().Be(200, "items win beyond the floor when the title is not kept whole");

        ScaffoldNavBarRow.ComputeTitleReservation(minimum: 200, natural: 500, keepWhole: true, available: 320)
                         .Should().Be(320, "never more than the row has beside the fixed buttons");

        ScaffoldNavBarRow.ComputeTitleReservation(minimum: 0, natural: double.PositiveInfinity, keepWhole: true, available: 320)
                         .Should().Be(0, "an unbounded natural width (odd title content) reserves nothing rather than everything");

        ScaffoldNavBarRow.ComputeTitleReservation(minimum: 200, natural: 100, keepWhole: true, available: 50)
                         .Should().Be(50, "the floor cannot exceed the row either");
    }

    [Fact(DisplayName = "The overflow menu lists folded primary items before the secondary ones")]
    public void OverflowMenuListsFoldedPrimaryFirst()
    {
        var page = new ContentPage();
        var folded = Item("Folded", priority: 9);
        page.ToolbarItems.Add(Item("Secondary", order: ToolbarItemOrder.Secondary));
        page.ToolbarItems.Add(folded);

        var strip = new ScaffoldToolbarItemsView { BindingContext = Host(page) };
        strip.OverflowButton.SqueezedItems = [folded];

        strip.OverflowButton.SqueezedItems.Concat(ScaffoldToolbarItemsObserver.SecondaryOf(page.ToolbarItems))
             .Select(item => item.Text)
             .Should().Equal("Folded", "Secondary");
    }

    [Fact(DisplayName = "The overflow menu lists secondary items by priority")]
    public void OverflowMenuListsSecondaryByPriority()
    {
        var page = new ContentPage();
        page.ToolbarItems.Add(Item("Primary"));
        page.ToolbarItems.Add(Item("Late", priority: 5, order: ToolbarItemOrder.Secondary));
        page.ToolbarItems.Add(Item("Early", priority: 1, order: ToolbarItemOrder.Secondary));

        var panel = new ScaffoldToolbarOverflowView(
            ScaffoldToolbarItemsObserver.SecondaryOf(page.ToolbarItems).ToList(),
            () => Task.CompletedTask
        );

        panel.Items.Select(item => item.Text).Should().Equal("Early", "Late");
    }

    [Fact(DisplayName = "The overflow menu's unstyled colors follow the theme; a set value pins them")]
    public void OverflowMenuColorsFollowTheThemeUnlessStyled()
    {
        var page = new ContentPage();
        page.ToolbarItems.Add(Item("Only", order: ToolbarItemOrder.Secondary));

        var panel = new ScaffoldToolbarOverflowView(
            ScaffoldToolbarItemsObserver.SecondaryOf(page.ToolbarItems).ToList(),
            () => Task.CompletedTask
        );

        // No Application here: the theme reads as light.
        panel.PanelBackground.Should().BeNull("unset means theme-following");
        panel.Background.Should().BeOfType<SolidColorBrush>().Which.Color.Should().Be(Color.FromArgb("#FAFFFFFF"));
        panel.TextColor.Should().BeNull();
        panel.EffectiveTextColor.Should().Be(ScaffoldNavBarDefaults.Foreground);

        panel.PanelBackground = Brush.Red;
        panel.Background.Should().BeSameAs(panel.PanelBackground, "a set panel background is used as is");

        panel.TextColor = Colors.Green;
        panel.EffectiveTextColor.Should().Be(Colors.Green);

        panel.MinimumWidthRequest.Should().Be(200, "the default menu minimum");
        panel.MaximumWidthRequest.Should().Be(280, "Material's menu maximum");
    }

    [Fact(DisplayName = "The overflow menu hangs below its button, end-aligned, flips above when it doesn't fit, and stays in the area")]
    public void OverflowPlacerHangsInwardBelowTheAnchor()
    {
        var area = new Rect(8, 60, 384, 700);
        var content = new Size(200, 100);
        var anchor = new Rect(340, 70, 44, 44);

        var ltr = new ScaffoldToolbarOverflowPlacer(isRtl: false).Place(area, content, anchor);
        ltr.Should().Be(new Rect(184, 114, 200, 100), "below the anchor, its right edge on the anchor's right edge");

        // In RTL the trailing button sits at the left: its LEFT edge is its end edge.
        var rtl = new ScaffoldToolbarOverflowPlacer(isRtl: true).Place(area, content, new Rect(16, 70, 44, 44));
        rtl.Should().Be(new Rect(16, 114, 200, 100), "in RTL the menu's start edge rides the anchor's left edge");

        var rtlClamped = new ScaffoldToolbarOverflowPlacer(isRtl: true).Place(area, content, anchor);
        rtlClamped.X.Should().Be(192, "a menu that would run off the end is pulled back into the area");

        var low = new ScaffoldToolbarOverflowPlacer(isRtl: false).Place(area, content, new Rect(340, 700, 44, 44));
        low.Y.Should().Be(600, "no room below: the menu flips above the anchor");

        var wide = new ScaffoldToolbarOverflowPlacer(isRtl: false).Place(area, new Size(500, 100), anchor);
        wide.X.Should().Be(area.Left, "a menu wider than the area is pinned to its start");

        var centered = new ScaffoldToolbarOverflowPlacer(isRtl: false).Place(area, content, null);
        centered.Should().Be(new Rect(100, 360, 200, 100), "with no anchor the menu is centered");
    }

    /// <summary>A command whose CanExecute is closed until opened.</summary>
    private sealed class GatedCommand : ICommand
    {
        private bool _open;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _open;

        public void Execute(object? parameter)
        {
        }

        public void Open()
        {
            _open = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
