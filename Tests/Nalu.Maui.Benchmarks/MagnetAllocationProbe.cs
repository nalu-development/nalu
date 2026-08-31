using Nalu.MagnetLayout.Engine;

namespace Nalu.Maui.Benchmarks;

/// <summary>
/// Diagnostic: allocation breakdown of a Magnet inflation (dotnet run -c Release -- --alloc-magnet).
/// </summary>
public static class MagnetAllocationProbe
{
    private class TestView(double width, double height) : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(width, height);
    }

    public static void Run()
    {
        // warm up
        Inflate(false);
        Inflate(true);
    }

    private static void Inflate(bool print)
    {
        var last = GC.GetAllocatedBytesForCurrentThread();

        void Phase(string name)
        {
            var now = GC.GetAllocatedBytesForCurrentThread();

            if (print)
            {
                Console.WriteLine($"{name,-40} {now - last,8} B");
            }

            last = now;
        }

        var views = new View[10];

        for (var i = 0; i < 10; i++)
        {
            views[i] = new TestView(20 + i, 20);
        }

        Phase("10 TestView (MAUI)");

        var grid = new Grid();
        for (var i = 0; i < 10; i++) { Grid.SetColumn(views[i], i); }
        Phase("Grid.SetColumn x10");
        for (var i = 0; i < 10; i++) { grid.Add(views[i]); }
        Phase("grid.Add x10");
        var gm = (Microsoft.Maui.Layouts.ILayoutManager) typeof(Layout).GetProperty("LayoutManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(grid)!;
        var gr = gm.Measure(500, double.PositiveInfinity);
        gm.ArrangeChildren(new Rect(Point.Zero, gr));
        Phase("grid measure+arrange");

        views = new View[10];
        for (var i = 0; i < 10; i++) { views[i] = new TestView(20 + i, 20); }
        Phase("10 TestView (MAUI) again");

        var chain = new MagnetChain { MagnetId = "row", Style = MagnetChainStyle.Packed };
        var definition = new MagnetDefinition().Add(chain);
        Phase("chain + definition");
        var magnet = new Magnet { Definition = definition };
        Phase("Magnet + Definition set");

        for (var i = 0; i < 10; i++)
        {
            Magnet.GetConstraints(views[i]).Id($"v{i}").Top(MagnetAnchor.Parent);
        }

        Phase("GetConstraints+Id+Top x10");

        for (var i = 0; i < 10; i++)
        {
            chain.Nodes.Add($"v{i}");
        }

        Phase("chain.Nodes.Add x10");

        for (var i = 0; i < 10; i++)
        {
            magnet.Add(views[i]);
        }

        Phase("magnet.Add x10 (bind/register)");
        Magnet.GetConstraints(views[0]).Left(MagnetAnchor.Parent).Bias(0, 0.5);
        Phase("Left+Bias on head");

        var nodes = definition.AllNodes.ToList();
        Phase("AllNodes.ToList");

        if (print)
        {
            const int reps = 20000;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var b0 = GC.GetAllocatedBytesForCurrentThread();
            for (var k = 0; k < reps; k++) { MagnetTapeCache.CreateKey(nodes); }
            var b1 = GC.GetAllocatedBytesForCurrentThread();
            Console.WriteLine($"CreateKey: {sw.Elapsed.TotalMicroseconds / reps:F2} us, {(b1 - b0) / reps} B");
            sw.Restart();
            b0 = GC.GetAllocatedBytesForCurrentThread();
            for (var k = 0; k < reps; k++) { MagnetCompiler.Compile(nodes); }
            b1 = GC.GetAllocatedBytesForCurrentThread();
            Console.WriteLine($"Compile:   {sw.Elapsed.TotalMicroseconds / reps:F2} us, {(b1 - b0) / reps} B");
            sw.Restart();
            b0 = GC.GetAllocatedBytesForCurrentThread();
            for (var k = 0; k < reps; k++) { MagnetCompiler.GetOrCompile(nodes); }
            b1 = GC.GetAllocatedBytesForCurrentThread();
            Console.WriteLine($"GetOrCompile (hit): {sw.Elapsed.TotalMicroseconds / reps:F2} us, {(b1 - b0) / reps} B");
        }

        var tape = MagnetCompiler.Compile(nodes);
        Phase($"Compile (ops={tape.Ops.Length}, slots={tape.ValueCount})");
        var engine = magnet.Engine;
        engine.Compile(nodes);
        Phase("engine.Compile (incl. 2nd compile)");
        var mm = (Microsoft.Maui.Layouts.ILayoutManager) typeof(Layout).GetProperty("LayoutManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(magnet)!;
        var r = mm.Measure(500, double.PositiveInfinity);
        mm.ArrangeChildren(new Rect(Point.Zero, r));
        Phase("magnet measure+arrange");
    }
}
