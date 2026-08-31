using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Maui.Layouts;

namespace Nalu.Maui.Benchmarks;

/// <summary>
/// The "credit card" cell of the sample app: image | name (+ star) / detail | money.
/// With Grid it takes three nested layouts (Grid + VerticalStackLayout + FlexLayout); with Magnet it is flat.
/// Both trees hold the same 5 leaf views.
/// </summary>
[MemoryDiagnoser]
public class MagnetCardBenchmarks
{
    private static readonly PropertyInfo _layoutManagerProperty = typeof(Layout).GetProperty("LayoutManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static ILayoutManager GetLayoutManager(Layout layout) => (ILayoutManager) _layoutManagerProperty.GetValue(layout)!;

    private class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
            => new(Math.Min(width, widthConstraint), Math.Min(height, heightConstraint));
    }

    private ILayoutManager? _grid;
    private ILayoutManager? _magnet;
    private View? _gridName;
    private View? _magnetName;
    private const int _iterations = 1000;

    private static Layout CreateGridCard() => CreateGridCard(out _);

    private static Layout CreateGridCard(out View nameView)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            )
        };

        var image = new TestView(60, 48) { Margin = 4 };
        var name = new TestView(221, 22);
        nameView = name;
        var star = new TestView(16, 16);
        var detail = new TestView(105, 16);
        var money = new TestView(98, 41);

        var flex = new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.NoWrap };
        FlexLayout.SetShrink(name, 1);
        flex.Add(name);
        flex.Add(star);

        var texts = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        texts.Add(flex);
        texts.Add(detail);
        Grid.SetColumn(texts, 1);
        Grid.SetColumn(money, 2);

        grid.Add(image);
        grid.Add(texts);
        grid.Add(money);

        return grid;
    }

    private static Layout CreateMagnetCard() => CreateMagnetCard(out _);

    private static Layout CreateMagnetCard(out View nameView)
    {
        const string p = MagnetAnchor.Parent;
        var magnet = new Magnet();

        var image = new TestView(60, 48);
        Magnet.GetConstraints(image).Id("CardImage").Left(p, margin: 4).VerticallyWithin(p, 4);
        // name + star: packed chain, the name shrinks (ellipsis) before pushing the star.
        var name = new TestView(221, 22);
        nameView = name;
        Magnet.GetConstraints(name).Id("CardName").Left("CardImage", MagnetPole.Right, 8).Top(p).Bias(0, 0.5);
        var star = new TestView(16, 16);
        Magnet.GetConstraints(star).Id("Starred").Left("CardName", MagnetPole.Right, 4).Right("Money", MagnetPole.Left, 8).VerticallyWithin("CardName");
        magnet.Definition = new MagnetDefinition().Add(new MagnetChain { MagnetId = "nameRow", Style = MagnetChainStyle.Packed }.With("CardName", "Starred"));
        var detail = new TestView(105, 16);
        Magnet.GetConstraints(detail).Id("CardDetail").Left("CardName", MagnetPole.Left).Top("CardName", MagnetPole.Bottom);
        var money = new TestView(98, 41);
        Magnet.GetConstraints(money).Id("Money").Right(p).VerticallyWithin(p).Size(MagnetSizing.Measured, MagnetSizing.Constraint);

        magnet.Add(image);
        magnet.Add(name);
        magnet.Add(star);
        magnet.Add(detail);
        magnet.Add(money);

        return magnet;
    }

    [GlobalSetup]
    public void Setup()
    {
        _grid = GetLayoutManager(CreateGridCard(out _gridName));
        _magnet = GetLayoutManager(CreateMagnetCard(out _magnetName));
    }

    /// <summary>Measure+arrange after a leaf invalidation (e.g. a text change) on every iteration: nothing is cached.</summary>
    [Benchmark]
    public void GridCardInvalidatedPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            _gridName!.WidthRequest = 200 + (i & 1); // size-request change: invalidates the leaf and propagates up
            var result = _grid!.Measure(400, double.PositiveInfinity);
            _grid.ArrangeChildren(new Rect(0, 0, 400, result.Height));
        }
    }

    /// <summary>Measure+arrange after a leaf invalidation (e.g. a text change) on every iteration: nothing is cached.</summary>
    [Benchmark]
    public void MagnetCardInvalidatedPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            _magnetName!.WidthRequest = 200 + (i & 1); // size-request change: invalidates the leaf and propagates up
            var result = _magnet!.Measure(400, double.PositiveInfinity);
            _magnet.ArrangeChildren(new Rect(0, 0, 400, result.Height));
        }
    }

    /// <summary>Repeated measure+arrange of the Grid card (constant bounds).</summary>
    [Benchmark]
    public void GridCardLayoutPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _grid!.Measure(400, double.PositiveInfinity);
            _grid.ArrangeChildren(new Rect(0, 0, 400, result.Height));
        }
    }

    /// <summary>Repeated measure+arrange of the Magnet card (constant bounds).</summary>
    [Benchmark]
    public void MagnetCardLayoutPerf()
    {
        for (var i = 0; i < _iterations; i++)
        {
            var result = _magnet!.Measure(400, double.PositiveInfinity);
            _magnet.ArrangeChildren(new Rect(0, 0, 400, result.Height));
        }
    }

    /// <summary>Inflation of 100 Grid cards (create + first measure/arrange).</summary>
    [Benchmark]
    public void GridCardInflationPerf()
    {
        for (var i = 0; i < 100; i++)
        {
            var manager = GetLayoutManager(CreateGridCard());
            var result = manager.Measure(400, double.PositiveInfinity);
            manager.ArrangeChildren(new Rect(0, 0, 400, result.Height));
        }
    }

    /// <summary>Inflation of 100 Magnet cards (create + compile + first measure/arrange).</summary>
    [Benchmark]
    public void MagnetCardInflationPerf()
    {
        for (var i = 0; i < 100; i++)
        {
            var manager = GetLayoutManager(CreateMagnetCard());
            var result = manager.Measure(400, double.PositiveInfinity);
            manager.ArrangeChildren(new Rect(0, 0, 400, result.Height));
        }
    }
}
