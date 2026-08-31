using System.Reflection;
using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout.Engine;

namespace Nalu.Maui.Test.Layouts.MagnetLayout;

/// <summary>
/// A fake view (NSubstitute-backed) with a fixed desired size which counts measure calls.
/// </summary>
internal sealed class FakeView
{
    private readonly double _width;
    private readonly double _height;
    private readonly bool _shrink;

    public FakeView(double width, double height, bool shrink = false, bool wraps = false)
    {
        _width = width;
        _height = height;
        _shrink = shrink;
        Wraps = wraps;
        View = Substitute.For<IView>();
        View.Visibility.Returns(_ => Visibility);
        View.DesiredSize.Returns(_ => DesiredSize);
        View.Measure(Arg.Any<double>(), Arg.Any<double>()).Returns(ci => Measure(ci.ArgAt<double>(0), ci.ArgAt<double>(1)));
        View.Arrange(Arg.Any<Rect>()).Returns(ci =>
        {
            Frame = ci.ArgAt<Rect>(0);

            return Frame.Size;
        });
    }

    public IView View { get; }

    /// <summary>
    /// Behaves like a wrapping label: content area is Width×Height, wraps when the width constraint is smaller.
    /// </summary>
    public bool Wraps { get; }

    public int MeasureCount { get; private set; }
    public List<(double W, double H)> Constraints { get; } = [];
    public Rect Frame { get; set; }
    public Size DesiredSize { get; private set; }
    public Visibility Visibility { get; set; } = Visibility.Visible;

    private Size Measure(double widthConstraint, double heightConstraint)
    {
        MeasureCount++;
        Constraints.Add((widthConstraint, heightConstraint));
        var w = _width;
        var h = _height;

        if (Wraps && widthConstraint < w)
        {
            var lines = Math.Ceiling(w / widthConstraint);
            w = widthConstraint;
            h = _height * lines;
        }
        else if (_shrink)
        {
            w = Math.Min(w, widthConstraint);
            h = Math.Min(h, heightConstraint);
        }

        DesiredSize = new Size(w, h);

        return DesiredSize;
    }
}

/// <summary>
/// Drives a <see cref="MagnetEngine" /> directly (no MAUI layout).
/// </summary>
internal sealed class EngineHarness
{
    private readonly List<MagnetNode> _nodes = [];
    private readonly Dictionary<string, FakeView> _views = new(StringComparer.Ordinal);

    public MagnetEngine Engine { get; } = new();

    public MagnetView View(string id, double width, double height, bool shrink = false, bool wraps = false)
    {
        var view = new FakeView(width, height, shrink, wraps);
        var node = new MagnetView { MagnetId = id, View = view.View };
        _views[id] = view;
        _nodes.Add(node);

        return node;
    }

    public MagnetView View(string id, FakeView view)
    {
        var node = new MagnetView { MagnetId = id, View = view.View };
        _views[id] = view;
        _nodes.Add(node);

        return node;
    }

    public T Add<T>(T node)
        where T : MagnetNode
    {
        _nodes.Add(node);

        return node;
    }

    public FakeView Fake(string id) => _views[id];

    public void Compile() => Engine.Compile(_nodes);

    public Size Measure(double w, double h)
    {
        if (!Engine.IsCompiled)
        {
            Compile();
        }

        return Engine.Measure(w, h);
    }

    /// <summary>
    /// Measure with the given constraints then arrange with the measured size (hug) — or with explicit bounds.
    /// </summary>
    public Size Layout(double wc, double hc, double? arrangeW = null, double? arrangeH = null)
    {
        var measured = Measure(wc, hc);
        var w = arrangeW ?? measured.Width;
        var h = arrangeH ?? measured.Height;
        var reuse = Math.Abs(w - measured.Width) < 0.5 || Math.Abs(w - wc) < 0.5;
        reuse &= Math.Abs(h - measured.Height) < 0.5 || Math.Abs(h - hc) < 0.5;
        Engine.Arrange(w, h, !reuse);

        return measured;
    }

    public Rect Frame(string id)
    {
        var index = _nodes.FindIndex(n => n.MagnetId == id);

        return Engine.GetFrame(index);
    }
}

internal static class MagnetTestExtensions
{
    private static readonly PropertyInfo _layoutManagerProperty = typeof(Layout).GetProperty("LayoutManager", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static ILayoutManager GetLayoutManager(this Layout layout) => (ILayoutManager) _layoutManagerProperty.GetValue(layout)!;

    public static void ShouldBe(this Rect rect, double x, double y, double w, double h, double precision = 0.01)
    {
        rect.X.Should().BeApproximately(x, precision, "X");
        rect.Y.Should().BeApproximately(y, precision, "Y");
        rect.Width.Should().BeApproximately(w, precision, "Width");
        rect.Height.Should().BeApproximately(h, precision, "Height");
    }
}
