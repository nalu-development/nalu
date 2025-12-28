using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Maui.Layouts;

namespace Nalu.Maui.Benchmarks;

/// <summary>
/// Benchmarks comparing a <see cref="Grid"/> using star columns (similar to "divide remaining space")
/// versus <see cref="HorizontalWrapLayout"/> using <see cref="WrapLayoutExpandMode.Divide"/>.
/// </summary>
/// <remarks>
/// These are not a pixel-perfect behavioral comparison in all scenarios; the intent is to compare performance
/// for a common "fixed + flexible" horizontal row layout.
/// </remarks>
[MemoryDiagnoser]
public class WrapLayoutBenchmarks
{
    private const int _iterations = 1000;

    private ILayoutManager? _gridLayoutManager;
    private ILayoutManager? _wrapLayoutManager;

    private static readonly PropertyInfo _layoutManagerProperty =
        typeof(Layout).GetProperty("LayoutManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static ILayoutManager GetLayoutManager(Layout layout) => (ILayoutManager)_layoutManagerProperty.GetValue(layout)!;

    private sealed class TestView(double width, double height, bool constant) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            if (!constant)
            {
                width += Random.Shared.Next(0, 10);
                height += Random.Shared.Next(0, 10);
            }

            return new Size(Math.Min(width, widthConstraint), Math.Min(height, heightConstraint));
        }
    }

    [Params(8)]
    public int FixedItemCount { get; set; }

    [Params(8)]
    public int ExpandingItemCount { get; set; }

    /// <summary>
    /// Overall width constraint used for each measure pass.
    /// </summary>
    [Params(500)]
    public int Width { get; set; }

    /// <summary>
    /// Overall height constraint used for each measure pass.
    /// </summary>
    [Params(60)]
    public int Height { get; set; }

    [GlobalSetup(Targets = [nameof(GridStar_Perf), nameof(WrapDivide_Perf)])]
    public void Setup_Constant() => SetupCore(constant: true);

    [GlobalSetup(Targets = [nameof(GridStar_DynamicMeasurePerf), nameof(WrapDivide_DynamicMeasurePerf)])]
    public void Setup_Dynamic() => SetupCore(constant: false);

    private void SetupCore(bool constant)
    {
        var grid = CreateGrid(constant);
        _gridLayoutManager = GetLayoutManager(grid);

        var wrap = CreateWrap(constant);
        _wrapLayoutManager = GetLayoutManager(wrap);
    }

    private Grid CreateGrid(bool constant)
    {
        // Auto columns for fixed items, Star columns for expanding items (ratio-based).
        var grid = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 0
        };

        var col = 0;

        for (var i = 0; i < FixedItemCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var view = new TestView(width: 80, height: 40, constant);
            grid.Add(view);
            Grid.SetColumn((BindableObject)view, col++);
        }

        for (var i = 0; i < ExpandingItemCount; i++)
        {
            // Use Star to divide remaining width between expanding items.
            // The ratio alternates 1,2,1,2,... to emulate a typical weighted distribution.
            var ratio = (i % 2 == 0) ? 1 : 2;
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ratio, GridUnitType.Star)));

            // Keep the "measured" width minimal so the allocation is dominated by the star distribution.
            var view = new TestView(width: 0, height: 40, constant);
            grid.Add(view);
            Grid.SetColumn((BindableObject)view, col++);
        }

        return grid;
    }

    private HorizontalWrapLayout CreateWrap(bool constant)
    {
        var wrap = new HorizontalWrapLayout
        {
            HorizontalSpacing = 0,
            VerticalSpacing = 0,
            ExpandMode = WrapLayoutExpandMode.Divide
        };

        for (var i = 0; i < FixedItemCount; i++)
        {
            var view = new TestView(width: 80, height: 40, constant);
            wrap.Add(view);
        }

        for (var i = 0; i < ExpandingItemCount; i++)
        {
            var ratio = (i % 2 == 0) ? 1 : 2;
            var view = new TestView(width: 0, height: 40, constant);
            WrapLayout.SetExpandRatio((BindableObject)view, ratio);
            wrap.Add(view);
        }

        return wrap;
    }

    [Benchmark]
    public void GridStar_Perf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var size = _gridLayoutManager!.Measure(Width, Height);
            _gridLayoutManager.ArrangeChildren(new Rect(Point.Zero, size));
        }
    }

    [Benchmark]
    public void WrapDivide_Perf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var size = _wrapLayoutManager!.Measure(Width, Height);
            _wrapLayoutManager.ArrangeChildren(new Rect(Point.Zero, size));
        }
    }

    [Benchmark]
    public void GridStar_DynamicMeasurePerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var size = _gridLayoutManager!.Measure(Width, Height);
            _gridLayoutManager.ArrangeChildren(new Rect(Point.Zero, size));
        }
    }

    [Benchmark]
    public void WrapDivide_DynamicMeasurePerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var size = _wrapLayoutManager!.Measure(Width, Height);
            _wrapLayoutManager.ArrangeChildren(new Rect(Point.Zero, size));
        }
    }
}


