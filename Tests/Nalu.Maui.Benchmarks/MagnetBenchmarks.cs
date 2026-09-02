using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Maui.Layouts;
using ILayout = Microsoft.Maui.ILayout;

namespace Nalu.Maui.Benchmarks;

// ReSharper disable GenericEnumeratorNotDisposed
[MemoryDiagnoser]
public class MagnetBenchmarks
{
    private ILayoutManager? _layoutManager;
    private Magnet? _magnet;
    private MagnetView? _animatedNode;
    private View? _invalidatedChild;
    private static readonly PropertyInfo _layoutManagerProperty = typeof(Layout).GetProperty("LayoutManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static ILayoutManager GetLayoutManager(Layout layout) => (ILayoutManager) _layoutManagerProperty.GetValue(layout)!;

    private class TestView(double width, double height, bool constant) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
            => constant ? new Size(width, height) : new Size(width + Random.Shared.Next(0, 10), height + Random.Shared.Next(0, 10));
    }

    /// <summary>
    /// The shared scenario: one row of 5 views, the middle one filling the remaining space
    /// (Grid: Auto,Auto,Star,Auto,Auto columns; Magnet: a horizontal chain whose middle member is "*").
    /// </summary>
    private static readonly (string Id, double Width, double Height)[] _children =
    [
        ("Icon", 24, 24),
        ("Title", 80, 20),
        ("Spacer", 10, 10),
        ("Badge", 16, 16),
        ("Money", 60, 28)
    ];

    private static void AddChildren(ILayout layout, bool constant)
    {
        for (var i = 0; i < _children.Length; i++)
        {
            var (id, w, h) = _children[i];
            var view = new TestView(w, h, constant);

            if (layout is Grid)
            {
                Grid.SetColumn(view, i);
            }
            else
            {
                Magnet.SetMagnetId(view, id);
            }

            layout.Add(view);
        }
    }

    private void AddChildren(ILayout layout, bool constant, out View secondChild)
    {
        AddChildren(layout, constant);
        secondChild = (View) layout[1];
    }

    private static Grid CreateGrid()
        => new()
        {
            RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto)),
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            )
        };

    private static Magnet CreateMagnet()
    {
        const string p = MagnetAnchor.Parent;
        // MAGNET_BENCH_GAP sizes the cost of the chain-Gap runtime ops (0 = elided entirely).
        var chain = new MagnetChain { MagnetId = "row", Gap = double.TryParse(Environment.GetEnvironmentVariable("MAGNET_BENCH_GAP"), out var g) ? g : 0 };
        var definition = new MagnetDefinition().Add(chain);

        for (var i = 0; i < _children.Length; i++)
        {
            var (id, _, _) = _children[i];
            var node = new MagnetView().Id(id).Top(p);

            if (i == 0)
            {
                node.Left(p);
            }

            if (i == _children.Length - 1)
            {
                node.Right(p);
            }

            if (i == 2)
            {
                node.WidthSizing = MagnetSizing.Constraint;
            }

            definition.Add(node);
            chain.Nodes.Add(id);
        }

        return new Magnet { Definition = definition };
    }

    private void Setup(bool magnet, bool constant)
    {
        ILayout layout = magnet ? CreateMagnet() : CreateGrid();
        AddChildren(layout, constant, out _invalidatedChild);
        _layoutManager = GetLayoutManager((Layout) layout);

        if (layout is Magnet m)
        {
            _magnet = m;
            _animatedNode = (MagnetView) m.Definition!.MagnetNodes[1];
        }
    }

    [GlobalSetup(Target = nameof(GridInvalidatedPerf))]
    public void GridSetup() => Setup(false, true);

    [GlobalSetup(Targets = [nameof(GridLayoutConstantMeasurePerf), nameof(GridChangingBoundsPerf)])]
    public void GridConstantSetup() => Setup(false, true);

    [GlobalSetup(Target = nameof(MagnetInvalidatedPerf))]
    public void MagnetSetup() => Setup(true, true);

    [GlobalSetup(Targets = [nameof(MagnetLayoutConstantMeasurePerf), nameof(MagnetChangingBoundsPerf), nameof(MagnetValuePatchPerf)])]
    public void MagnetConstantSetup() => Setup(true, true);

    private const int _iterations = 1000;

    /// <summary>Measure+arrange after a child invalidation on every iteration (e.g. a text change): nothing is cached.</summary>
    [Benchmark]
    public void GridInvalidatedPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            _invalidatedChild!.WidthRequest = 80 + (i & 1);
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }

    /// <summary>Measure+arrange after a child invalidation on every iteration (e.g. a text change): nothing is cached.</summary>
    [Benchmark]
    public void MagnetInvalidatedPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            _invalidatedChild!.WidthRequest = 80 + (i & 1);
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }

    /// <summary>Repeated measure+arrange with constant bounds and no invalidation (MAUI re-measures often without changes).</summary>
    [Benchmark]
    public void GridLayoutConstantMeasurePerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }

    /// <summary>Repeated measure+arrange with constant bounds and no invalidation.</summary>
    [Benchmark]
    public void MagnetLayoutConstantMeasurePerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }

    /// <summary>Measure with changing bounds (rotation scenario).</summary>
    [Benchmark]
    public void GridChangingBoundsPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var width = 400 + ((i & 1) * 300);
            var result = _layoutManager!.Measure(width, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, width, result.Height));
        }
    }

    /// <summary>Measure with changing bounds (rotation scenario).</summary>
    [Benchmark]
    public void MagnetChangingBoundsPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var width = 400 + ((i & 1) * 300);
            var result = _layoutManager!.Measure(width, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, width, result.Height));
        }
    }

    /// <summary>Value patch (animated margin) + re-execute.</summary>
    [Benchmark]
    public void MagnetValuePatchPerf()
    {
        var node = _animatedNode!;

        for (var i = 0; i < _iterations; i++)
        {
            node.LeftTo = new MagnetAnchor(MagnetAnchor.Parent, MagnetPole.Left, i & 15);
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }

    private const int _inflationChildren = 10;

    /// <summary>Inflation cost of a single-row Grid with 10 Auto columns × 100 instances (measure + arrange included).</summary>
    [Benchmark]
    public void GridInflationPerf()
    {
        for (var i = 0; i < 100; i++)
        {
            var grid = new Grid { RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto)) };
            var columns = new ColumnDefinitionCollection();

            for (var v = 0; v < _inflationChildren; v++)
            {
                columns.Add(new ColumnDefinition(GridLength.Auto));
            }

            grid.ColumnDefinitions = columns;

            for (var v = 0; v < _inflationChildren; v++)
            {
                var view = new TestView(20 + v, 20, true);
                Grid.SetColumn(view, v);
                grid.Add(view);
            }

            var manager = GetLayoutManager(grid);
            var result = manager.Measure(500, double.PositiveInfinity);
            manager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }

    /// <summary>Inflation cost of a horizontal MagnetChain with the same 10 children × 100 instances (compile, measure + arrange included).</summary>
    [Benchmark]
    public void MagnetInflationPerf()
    {
        for (var i = 0; i < 100; i++)
        {
            var chain = new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed };
            var magnet = new Magnet { Definition = new MagnetDefinition().Add(chain) };

            for (var v = 0; v < _inflationChildren; v++)
            {
                var view = new TestView(20 + v, 20, true);
                var id = $"v{v}";
                Magnet.GetConstraints(view).Id(id).Top(MagnetAnchor.Parent);
                chain.Nodes.Add(id);
                magnet.Add(view);
            }

            Magnet.GetConstraints((BindableObject) magnet[0]).Left(MagnetAnchor.Parent).Bias(0, 0.5);

            var manager = GetLayoutManager(magnet);
            var result = manager.Measure(500, double.PositiveInfinity);
            manager.ArrangeChildren(new Rect(Point.Zero, result));
        }
    }

    public void MagnetInvalidatedPerf(int iterations)
    {
        Setup(true, false);

        for (var i = 0; i < iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }

    public void MagnetLayoutConstantMeasurePerf(int iterations)
    {
        Setup(true, true);

        for (var i = 0; i < iterations; i++)
        {
            var result = _layoutManager!.Measure(500, 500);
            _layoutManager.ArrangeChildren(new Rect(0, 0, 500, result.Height));
        }
    }
}
