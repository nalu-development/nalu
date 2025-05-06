using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout;
using ILayout = Microsoft.Maui.ILayout;

namespace Nalu.Maui.Benchmarks;

// ReSharper disable GenericEnumeratorNotDisposed
[MemoryDiagnoser]
[InvocationCount(10)]
public class MagnetBenchmarks
{
    private ILayoutManager? _layoutManager;
    private static readonly PropertyInfo _layoutManagerProperty = typeof(Layout).GetProperty("LayoutManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static ILayoutManager GetLayoutManager(Layout layout) => (ILayoutManager) _layoutManagerProperty.GetValue(layout)!;

    private class TestView(double width, double height, bool constant) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
            => constant ? new Size(width, height) : new Size(width + Random.Shared.Next(0, 10), height + Random.Shared.Next(0, 10));
    }

    private void BasicSetup(ILayout layout, bool constant = true)
    {
        var cardImage = CreateTestView("CardImage", 60, 48, constant);
        SetGridLocation(cardImage, rowSpan: 2);
        var cardName = CreateTestView("CardName", 80, 20, constant);
        SetGridLocation(cardName, col: 1);
        var cardDetail = CreateTestView("CardDetail", 70, 16, constant);
        SetGridLocation(cardDetail, col: 1, row: 1, colSpan: 2);
        var money = CreateTestView("Money", 80, 28, constant);
        SetGridLocation(money, col: 3, rowSpan: 2);
        var starred = CreateTestView("Starred", 16, 16, constant);
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

    private static IView CreateTestView(string id, double width, double height, bool constant)
    {
        var view = new TestView(width, height, constant);
        Magnet.SetStageId((BindableObject)view, id);

        return view;
    }

    [GlobalSetup(Target = nameof(GridLayoutPerf))]
    public void GridSetup()
    {
        var grid = CreateGrid();

        BasicSetup(grid, false);

        _layoutManager = GetLayoutManager(grid);
    }

    [GlobalSetup(Target = nameof(GridLayoutConstantMeasurePerf))]
    public void GridConstantSetup()
    {
        var grid = CreateGrid();

        BasicSetup(grid);

        _layoutManager = GetLayoutManager(grid);
    }

    private static Grid CreateGrid()
    {
        var grid = new Grid
                   {
                       RowDefinitions = new RowDefinitionCollection(
                           new RowDefinition { Height = GridLength.Star },
                           new RowDefinition { Height = new GridLength(0.85, GridUnitType.Star) }
                       ),
                       ColumnDefinitions = new ColumnDefinitionCollection(
                           new ColumnDefinition(GridLength.Auto),
                           new ColumnDefinition(GridLength.Auto),
                           new ColumnDefinition(GridLength.Star),
                           new ColumnDefinition(GridLength.Auto)
                       )
                   };
        return grid;
    }

    [GlobalSetup(Target = nameof(MagnetLayoutPerf))]
    public void MagnetSetup()
    {
        var magnet = CreateMagnet();

        BasicSetup(magnet, false);
        
        _layoutManager = GetLayoutManager(magnet);
    }

    [GlobalSetup(Target = nameof(MagnetLayoutConstantMeasurePerf))]
    public void MagnetConstantSetup()
    {
        var magnet = CreateMagnet();

        BasicSetup(magnet);
        
        _layoutManager = GetLayoutManager(magnet);
    }

    private static Magnet CreateMagnet()
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
        return magnet;
    }

    private const int _iterations = 2000;

    [Benchmark]
    public void GridLayoutPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }

    [Benchmark]
    public void MagnetLayoutPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }

    [Benchmark]
    public void GridLayoutConstantMeasurePerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }

    [Benchmark]
    public void MagnetLayoutConstantMeasurePerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }
}
