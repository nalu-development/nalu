namespace Nalu.Internals;

/// <summary>
/// The Brush leg of the scroll-value interpolations: lerps solid ↔ solid, solid ↔ gradient and
/// gradient ↔ gradient endpoint pairs (linear or radial — both sides must be the same gradient
/// type; stop counts and positions may differ). One instance per value converter.
/// </summary>
/// <remarks>
/// The endpoint pair is normalized ONCE into a plan (the union of both sides' stop offsets, each
/// side's color sampled at every union offset, per-side geometry): per evaluation only the lerp
/// runs. By default every evaluation emits a FRESH brush instance — the plain MAUI binding
/// behavior, safe on any Brush-typed target.
/// </remarks>
internal sealed class ScrollValueBrushInterpolator
{
    /// <summary>
    /// EXPERIMENTAL opt-in: set to true to reuse ONE output brush instance per binding, mutated
    /// in place — zero allocations per scroll frame, but the target must track brush-content
    /// changes to repaint (Background/Stroke/Fill do; custom Brush properties may not), and each
    /// gradient-stop set raises its own invalidation. Default false: a fresh instance per
    /// evaluation, the safe behavior.
    /// </summary>
    public static bool ReuseInstancesByDefault { get; set; }

    /// <summary>Per-instance override of <see cref="ReuseInstancesByDefault"/> (tests; null = follow the default).</summary>
    public bool? ReuseOverride { get; init; }

    private object? _planFrom;
    private object? _planTo;
    private Plan? _plan;
    private Brush? _retained;
    private GradientStop[]? _retainedStops;

    public Brush Materialize(object? from, object? to, double t)
    {
        if (_plan is null || !ReferenceEquals(from, _planFrom) || !ReferenceEquals(to, _planTo))
        {
            _plan = BuildPlan(from, to);
            _planFrom = from;
            _planTo = to;
            _retained = null;
            _retainedStops = null;
        }

        var plan = _plan;
        var reuse = ReuseOverride ?? ReuseInstancesByDefault;

        if (plan.Offsets is null)
        {
            var color = ScrollValueMath.LerpColor(plan.FromColors[0], plan.ToColors[0], t);

            if (!reuse)
            {
                return new SolidColorBrush(color);
            }

            var solid = _retained as SolidColorBrush ?? new SolidColorBrush();
            _retained = solid;
            solid.Color = color;

            return solid;
        }

        // Colors clamp inside LerpColor; geometry clamps here (Extend never extrapolates brushes).
        var gt = Math.Clamp(t, 0, 1);

        if (!reuse || _retained is null)
        {
            var stops = new GradientStop[plan.Offsets.Length];

            for (var i = 0; i < stops.Length; i++)
            {
                stops[i] = new GradientStop(ScrollValueMath.LerpColor(plan.FromColors[i], plan.ToColors[i], t), plan.Offsets[i]);
            }

            var collection = new GradientStopCollection();

            foreach (var stop in stops)
            {
                collection.Add(stop);
            }

            GradientBrush brush = plan.Radial
                ? new RadialGradientBrush(collection)
                : new LinearGradientBrush(collection);

            ApplyGeometry(brush, plan, gt);

            if (!reuse)
            {
                return brush;
            }

            _retained = brush;
            _retainedStops = stops;

            return brush;
        }

        // In-place mutation: offsets are fixed by the plan, only colors and geometry move.
        // Each stop-color set notifies the attached target through the gradient invalidation
        // channel — that is what repaints without a new instance.
        var retainedStops = _retainedStops!;

        for (var i = 0; i < retainedStops.Length; i++)
        {
            retainedStops[i].Color = ScrollValueMath.LerpColor(plan.FromColors[i], plan.ToColors[i], t);
        }

        ApplyGeometry((GradientBrush)_retained, plan, gt);

        return _retained;
    }

    private static void ApplyGeometry(GradientBrush brush, Plan plan, double t)
    {
        if (brush is LinearGradientBrush linear)
        {
            linear.StartPoint = LerpPoint(plan.FromStart, plan.ToStart, t);
            linear.EndPoint = LerpPoint(plan.FromEnd, plan.ToEnd, t);
        }
        else if (brush is RadialGradientBrush radial)
        {
            radial.Center = LerpPoint(plan.FromStart, plan.ToStart, t);
            radial.Radius = ScrollValueMath.Lerp(plan.FromRadius, plan.ToRadius, t);
        }
    }

    private static Point LerpPoint(Point from, Point to, double t)
        => new(ScrollValueMath.Lerp(from.X, to.X, t), ScrollValueMath.Lerp(from.Y, to.Y, t));

    /// <summary>Endpoint pair normalized for lerping: null offsets = solid ↔ solid.</summary>
    private sealed class Plan
    {
        public required Color[] FromColors { get; init; }

        public required Color[] ToColors { get; init; }

        public float[]? Offsets { get; init; }

        public bool Radial { get; init; }

        // Per-side geometry: Start doubles as the radial Center.
        public Point FromStart { get; init; }

        public Point ToStart { get; init; }

        public Point FromEnd { get; init; }

        public Point ToEnd { get; init; }

        public double FromRadius { get; init; }

        public double ToRadius { get; init; }
    }

    private static Plan BuildPlan(object? fromValue, object? toValue)
    {
        var from = Coerce(fromValue);
        var to = Coerce(toValue);

        if (from is Color fromSolid && to is Color toSolid)
        {
            return new Plan { FromColors = [fromSolid], ToColors = [toSolid] };
        }

        var fromGradient = from as GradientBrush;
        var toGradient = to as GradientBrush;

        if (fromGradient is not null && toGradient is not null && fromGradient is RadialGradientBrush != toGradient is RadialGradientBrush)
        {
            throw new InvalidOperationException(
                $"ScrollValue gradient endpoints must be the same gradient type: cannot interpolate {fromGradient.GetType().Name} ↔ {toGradient.GetType().Name}.");
        }

        var shape = fromGradient ?? toGradient!;

        // The union of both sides' stop offsets: each side is sampled at every union offset, so
        // differing stop counts and positions still pair up (a solid side samples constant).
        var offsets = SortedStops(fromGradient)
                      .Concat(SortedStops(toGradient))
                      .Select(s => s.Offset)
                      .Distinct()
                      .Order()
                      .ToArray();

        if (offsets.Length == 0)
        {
            offsets = [0f, 1f];
        }

        var (fromStart, fromEnd, fromRadius) = Geometry(fromGradient ?? shape);
        var (toStart, toEnd, toRadius) = Geometry(toGradient ?? shape);

        return new Plan
        {
            FromColors = [.. offsets.Select(offset => Evaluate(from, offset))],
            ToColors = [.. offsets.Select(offset => Evaluate(to, offset))],
            Offsets = offsets,
            Radial = shape is RadialGradientBrush,
            FromStart = fromStart,
            ToStart = toStart,
            FromEnd = fromEnd,
            ToEnd = toEnd,
            FromRadius = fromRadius,
            ToRadius = toRadius
        };
    }

    /// <summary>Endpoint → <see cref="Color"/> (solid) or <see cref="GradientBrush"/>.</summary>
    private static object Coerce(object? value)
        => value switch
        {
            Color color => color,
            SolidColorBrush solid => solid.Color,
            LinearGradientBrush or RadialGradientBrush => value,
            string text when Color.TryParse(text, out var parsed) => parsed,
            null => Colors.Transparent,
            _ => throw new InvalidOperationException(
                $"ScrollValue endpoint '{value}' is not a color or a supported brush (solid, linear gradient or radial gradient).")
        };

    private static (Point Start, Point End, double Radius) Geometry(GradientBrush brush)
        => brush switch
        {
            LinearGradientBrush linear => (linear.StartPoint, linear.EndPoint, 0),
            RadialGradientBrush radial => (radial.Center, default, radial.Radius),
            _ => (default, default, 0)
        };

    private static GradientStop[] SortedStops(GradientBrush? brush)
        => brush is null ? [] : [.. brush.GradientStops.OrderBy(s => s.Offset)];

    /// <summary>The side's color at an offset: piecewise linear between its own stops, clamped outside them.</summary>
    private static Color Evaluate(object side, float offset)
    {
        if (side is Color solid)
        {
            return solid;
        }

        var stops = SortedStops((GradientBrush)side);

        if (stops.Length == 0)
        {
            return Colors.Transparent;
        }

        if (offset <= stops[0].Offset)
        {
            return ColorOf(stops[0]);
        }

        if (offset >= stops[^1].Offset)
        {
            return ColorOf(stops[^1]);
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (offset <= stops[i].Offset)
            {
                var span = stops[i].Offset - stops[i - 1].Offset;
                var t = span == 0 ? 1 : (offset - stops[i - 1].Offset) / span;

                return ScrollValueMath.LerpColor(ColorOf(stops[i - 1]), ColorOf(stops[i]), t);
            }
        }

        return ColorOf(stops[^1]);
    }

    /// <summary>An unset stop color (XAML allows it) reads as transparent instead of faulting the lerp.</summary>
    private static Color ColorOf(GradientStop stop) => stop.Color ?? Colors.Transparent;
}
