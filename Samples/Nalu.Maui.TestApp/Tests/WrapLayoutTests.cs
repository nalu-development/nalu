using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Wrap Layout Tests")]
public class WrapLayoutTestsPage : ContentPage
{
    private static BoxView FixedBox(string automationId, double width, double height, Color color) => new()
    {
        AutomationId = automationId,
        WidthRequest = width,
        HeightRequest = height,
        Color = color
    };

    // Expanding items must NOT have an explicit WidthRequest (an explicit width wins over
    // Fill alignment during arrange, and MinimumWidthRequest does not raise a BoxView's
    // desired size): wrap a fixed-size BoxView in a Grid so the Grid measures at the natural
    // width but still stretches to the expanded arrange width.
    private static Grid ExpandingBox(string automationId, double naturalWidth, double height, Color color, double expandRatio)
    {
        var grid = new Grid { AutomationId = automationId, BackgroundColor = color };
        grid.Add(new BoxView
            {
                WidthRequest = naturalWidth,
                HeightRequest = height,
                HorizontalOptions = LayoutOptions.Start,
                Color = color
            }
        );
        WrapLayout.SetExpandRatio(grid, expandRatio);

        return grid;
    }

    public WrapLayoutTestsPage()
    {
        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };

        // --- Horizontal wrapping + spacing --------------------------------------------------
        // 320 wide: two 120-wide items (+10 spacing = 250) fit, the third wraps to row 2.
        var hWrap = new HorizontalWrapLayout
        {
            AutomationId = "HWrap",
            WidthRequest = 320,
            HorizontalOptions = LayoutOptions.Start,
            HorizontalSpacing = 10,
            VerticalSpacing = 8,
            BackgroundColor = Colors.LightGray
        };
        hWrap.Add(FixedBox("HWrapItem1", 120, 40, Colors.IndianRed));
        hWrap.Add(FixedBox("HWrapItem2", 120, 40, Colors.SeaGreen));
        hWrap.Add(FixedBox("HWrapItem3", 120, 40, Colors.SteelBlue));
        stack.Add(hWrap);

        // --- ItemsAlignment -----------------------------------------------------------------
        foreach (var alignment in new[] { WrapLayoutItemsAlignment.Start, WrapLayoutItemsAlignment.Center, WrapLayoutItemsAlignment.End })
        {
            var alignLayout = new HorizontalWrapLayout
            {
                AutomationId = $"Align{alignment}Layout",
                WidthRequest = 320,
                HorizontalOptions = LayoutOptions.Start,
                ItemsAlignment = alignment,
                BackgroundColor = Colors.LightGray
            };
            alignLayout.Add(FixedBox($"Align{alignment}Item", 100, 30, Colors.DarkOrange));
            stack.Add(alignLayout);
        }

        // --- ExpandMode.Distribute ----------------------------------------------------------
        // 320 wide, spacing 10, two items with natural width 60 and ratio 1:
        // remaining 190 is split evenly -> both arranged at 155.
        var distributeLayout = new HorizontalWrapLayout
        {
            AutomationId = "DistributeLayout",
            WidthRequest = 320,
            HorizontalOptions = LayoutOptions.Start,
            HorizontalSpacing = 10,
            ExpandMode = WrapLayoutExpandMode.Distribute,
            BackgroundColor = Colors.LightGray
        };
        distributeLayout.Add(ExpandingBox("DistItemA", 60, 30, Colors.IndianRed, 1));
        distributeLayout.Add(ExpandingBox("DistItemB", 60, 30, Colors.SeaGreen, 1));
        stack.Add(distributeLayout);

        // --- ExpandMode.DistributeProportionally --------------------------------------------
        // 300 wide, no spacing, natural widths 50 and 100 with ratio 1:
        // remaining 150 split by size -> 100 and 200 (B stays twice as wide as A).
        var proportionalLayout = new HorizontalWrapLayout
        {
            AutomationId = "ProportionalLayout",
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            ExpandMode = WrapLayoutExpandMode.DistributeProportionally,
            BackgroundColor = Colors.LightGray
        };
        proportionalLayout.Add(ExpandingBox("PropItemA", 50, 30, Colors.IndianRed, 1));
        proportionalLayout.Add(ExpandingBox("PropItemB", 100, 30, Colors.SeaGreen, 1));
        stack.Add(proportionalLayout);

        // --- ExpandMode.Divide --------------------------------------------------------------
        // 300 wide, no spacing, natural widths 50 and 100 with ratio 1:
        // Divide replaces the measured size -> both arranged at 150 (equal widths).
        var divideLayout = new HorizontalWrapLayout
        {
            AutomationId = "DivideLayout",
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Start,
            ExpandMode = WrapLayoutExpandMode.Divide,
            BackgroundColor = Colors.LightGray
        };
        divideLayout.Add(ExpandingBox("DivItemA", 50, 30, Colors.IndianRed, 1));
        divideLayout.Add(ExpandingBox("DivItemB", 100, 30, Colors.SeaGreen, 1));
        stack.Add(divideLayout);

        // --- Vertical wrapping + spacing ----------------------------------------------------
        // 150 tall: two 60-tall items (+10 spacing = 130) fit, the third wraps to column 2.
        var vWrap = new VerticalWrapLayout
        {
            AutomationId = "VWrap",
            HeightRequest = 150,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            VerticalSpacing = 10,
            HorizontalSpacing = 12,
            BackgroundColor = Colors.LightGray
        };
        vWrap.Add(FixedBox("VWrapItem1", 80, 60, Colors.IndianRed));
        vWrap.Add(FixedBox("VWrapItem2", 80, 60, Colors.SeaGreen));
        vWrap.Add(FixedBox("VWrapItem3", 80, 60, Colors.SteelBlue));
        stack.Add(vWrap);

        Content = new ScrollView { Content = stack };
    }
}
