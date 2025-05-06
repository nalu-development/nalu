using System.Reflection;
using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout;
using ILayout = Microsoft.Maui.ILayout;

namespace Nalu.Maui.Test;

public class MagnetBenchmarksTests
{
    private ILayoutManager? _layoutManager;
    private static readonly PropertyInfo _layoutManagerProperty = typeof(Layout).GetProperty("LayoutManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static ILayoutManager GetLayoutManager(Layout layout) => (ILayoutManager) _layoutManagerProperty.GetValue(layout)!;

    private class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
            => new(width + Random.Shared.Next(0, 10), height + Random.Shared.Next(0, 10));
    }

    private void BasicSetup(ILayout layout)
    {
        var cardImage = CreateTestView("CardImage", 60, 48);
        SetGridLocation(cardImage, rowSpan: 2);
        var cardName = CreateTestView("CardName", 80, 20);
        SetGridLocation(cardName, col: 1);
        var cardDetail = CreateTestView("CardDetail", 70, 16);
        SetGridLocation(cardDetail, col: 1, row: 1, colSpan: 2);
        var money = CreateTestView("Money", 80, 28);
        SetGridLocation(money, col: 3, rowSpan: 2);
        var starred = CreateTestView("Starred", 16, 16);
        SetGridLocation(starred, col: 2);

        layout.Add(cardImage);
        layout.Add(cardName);
        layout.Add(cardDetail);
        layout.Add(money);
        layout.Add(starred);

        return;

        static void SetGridLocation(IView view, int row = 0, int col = 0, int rowSpan = 1, int colSpan = 1)
        {
            var bo = (BindableObject) view;
            Grid.SetRow(bo, row);
            Grid.SetRowSpan(bo, rowSpan);
            Grid.SetColumn(bo, col);
            Grid.SetColumnSpan(bo, colSpan);
        }
    }

    private static IView CreateTestView(string id, double width, double height)
    {
        var view = new TestView(width, height);
        Magnet.SetStageId((BindableObject)view, id);

        return view;
    }

    private void MagnetSetup()
    {
        var magnet = new Magnet
                     {
                         Stage = new MagnetStage
                                 {
                                     new MagnetView
                                     {
                                         Id = "CardImage",
                                         Margin = 4,
                                         TopTo = "Stage.Top",
                                         BottomTo = "Stage.Bottom",
                                         LeftTo = "Stage.Left",
                                     },
                                     new MagnetView
                                     {
                                         Id = "CardName",
                                         TopTo = "Stage.Top",
                                         BottomTo = "CardDetail.Top!",
                                         Margin = new Thickness(8, 0, 0, 0),
                                         Width = "1~",
                                         HorizontalBias = 0,
                                         LeftTo = "CardImage.Right",
                                         RightTo = "Starred.Left!",
                                     },
                                     new MagnetView
                                     {
                                         Id = "Starred",
                                         LeftTo = "CardImage.Right!",
                                         RightTo = "Money.Left",
                                         TopTo = "CardName.Top",
                                         BottomTo = "CardName.Bottom",
                                         Margin = new Thickness(0, 0, 8, 0),
                                     },
                                     new MagnetView
                                     {
                                         Id = "CardDetail",
                                         TopTo = "CardName.Bottom!",
                                         BottomTo = "Stage.Bottom",
                                         LeftTo = "CardImage.Left",
                                     },
                                     new MagnetView
                                     {
                                         Id = "Money",
                                         Height = "*",
                                         TopTo = "Stage.Top",
                                         BottomTo = "Stage.Bottom",
                                         RightTo = "Stage.Right!"
                                     }
                                 }
                     };

        BasicSetup(magnet);
        
        _layoutManager = GetLayoutManager(magnet);
    }

    public MagnetBenchmarksTests()
    {
        MagnetSetup();
    }
    
    [Fact]
    public void MagnetLayoutPerf()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }
}
