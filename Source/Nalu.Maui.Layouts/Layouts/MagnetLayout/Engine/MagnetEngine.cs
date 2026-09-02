using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalu.MagnetLayout.Engine;

/// <summary>
/// Which MeasureChild ops an execution runs. IMMEDIATE ops (Dst = -1) measure during the measure pass with
/// constraints valid at the requested size; DEFERRED ops (Dst = -2) have stage-dependent constraints and only
/// measure at arrange time, when the real solution is known.
/// </summary>
internal enum MeasurePass : byte
{
    None,
    Deferred,
    Immediate,
    All
}

/// <summary>
/// Owns the compiled tape and the value array of one <see cref="Magnet" /> instance and executes it.
/// </summary>
internal sealed class MagnetEngine
{
    private MagnetTape? _tape;
    private MagnetNode[] _nodes = [];
    private Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private double[] _values = [];
    private double[] _slopes = [];
    private double[] _feedbackPrev = [];
    private byte[] _vis = [];
    private IView?[] _views = [];
    private IView?[] _bound = [];
    private readonly Dictionary<MagnetView, IView?> _bindings = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<MagnetNode, int> _indexOf = new(ReferenceEqualityComparer.Instance);
    private double _eval;
    private bool _deferredMeasured;
    private HashSet<int>? _forcedCollapsed;

    /// <summary>
    /// Gets whether a tape is compiled.
    /// </summary>
    public bool IsCompiled => _tape is not null;

    /// <summary>
    /// Gets the compiled tape (for diagnostics/tests).
    /// </summary>
    public MagnetTape? Tape => _tape;

    /// <summary>
    /// Gets the nodes in compiled order.
    /// </summary>
    public MagnetNode[] Nodes => _nodes;

    /// <summary>
    /// The value array (tests/transitions).
    /// </summary>
    public double[] Values => _values;

    /// <summary>
    /// The stage size arguments of the last measure execution.
    /// </summary>
    public Size LastMeasureArgs { get; private set; } = new(double.NaN, double.NaN);

    /// <summary>
    /// The result of the last measure execution.
    /// </summary>
    public Size LastMeasured { get; private set; } = new(double.NaN, double.NaN);

    /// <summary>
    /// Whether a measure execution happened since the last compile/patch.
    /// </summary>
    public bool HasMeasured { get; private set; }

    /// <summary>
    /// Compiles the nodes and patches all input values.
    /// </summary>
    public void Compile(IReadOnlyList<MagnetNode> nodes)
    {
        var tape = MagnetCompiler.GetOrCompile(nodes);
        _tape = tape;
        _nodes = nodes as MagnetNode[] ?? nodes.ToArray();
        _ids = new Dictionary<string, int>(_nodes.Length, StringComparer.Ordinal);
        _indexOf.Clear();

        for (var i = 0; i < _nodes.Length; i++)
        {
            _ids[_nodes[i].MagnetId!] = i;
            _indexOf[_nodes[i]] = i;
        }

        if (_values.Length != tape.ValueCount)
        {
            _values = new double[tape.ValueCount];
            _slopes = new double[tape.ValueCount];
        }
        else
        {
            Array.Clear(_values);
            Array.Clear(_slopes);
        }

        if (_vis.Length != _nodes.Length)
        {
            _vis = new byte[_nodes.Length];
            _views = new IView?[_nodes.Length];
            _bound = new IView?[_nodes.Length];
        }

        // Project the per-layout view bindings onto the compiled order (and drop bindings of swapped-out nodes).
        Array.Clear(_bound);
        List<MagnetView>? stale = null;

        foreach (var (node, view) in _bindings)
        {
            if (_indexOf.TryGetValue(node, out var index))
            {
                _bound[index] = view;
            }
            else
            {
                (stale ??= []).Add(node);
            }
        }

        if (stale is not null)
        {
            foreach (var node in stale)
            {
                _bindings.Remove(node);
            }
        }

        if (_feedbackPrev.Length != tape.FeedbackSlots.Length)
        {
            _feedbackPrev = new double[tape.FeedbackSlots.Length];
        }

        _values[MagnetTape.One] = 1;
        _values[MagnetTape.PosInf] = double.PositiveInfinity;
        _values[MagnetTape.NegInf] = double.NegativeInfinity;

        PatchValues();
    }

    /// <summary>
    /// Re-copies every patchable input from the node properties.
    /// </summary>
    public void PatchValues()
    {
        var tape = _tape ?? throw new InvalidOperationException("Not compiled.");
        var values = _values;
        var patches = tape.Patches;

        for (var i = 0; i < patches.Length; i++)
        {
            ref readonly var p = ref patches[i];
            values[p.Slot] = ReadInput(_nodes[p.Node], p.Kind, p.Aux);
        }

        HasMeasured = false;
        _deferredMeasured = false;
    }

    private double ReadInput(MagnetNode node, PatchKind kind, int aux)
    {
        switch (kind)
        {
            case PatchKind.AnchorMargin:
                return ((MagnetView) node).GetAnchor((MagnetPole) aux)?.Margin ?? 0;

            case PatchKind.AnchorGoneMargin:
                return ((MagnetView) node).GetAnchor((MagnetPole) aux)?.EffectiveGoneMargin ?? 0;

            case PatchKind.SizeValue:
            {
                var size = GetSize((MagnetView) node, aux);

                return size.Unit == MagnetSizingUnit.Measured && size.Value <= 0 ? 1 : size.Value;
            }

            case PatchKind.SizeMin:
                return GetSize((MagnetView) node, aux).Min;

            case PatchKind.SizeMax:
                return GetSize((MagnetView) node, aux).Max;

            case PatchKind.BiasX:
                return ((MagnetView) node).HorizontalBias;

            case PatchKind.BiasY:
                return ((MagnetView) node).VerticalBias;

            case PatchKind.BarrierMargin:
                return ((MagnetBarrier) node).Margin;

            case PatchKind.GuidelinePercent:
                return ((MagnetGuideline) node).Percent;

            case PatchKind.GuidelinePosition:
                return ((MagnetGuideline) node).Position;

            case PatchKind.ChainWeight:
                return ChainWeight((MagnetChain) node, aux);

            case PatchKind.ChainGap:
                return ((MagnetChain) node).Gap;

            default:
                throw new NotSupportedException(kind.ToString());
        }
    }

    private static MagnetSizing GetSize(MagnetView view, int axis) => axis == 0 ? view.WidthSizing : view.HeightSizing;

    /// <summary>
    /// The raw weight of a chain member (validated). Fractions are computed at runtime by the tape so that
    /// collapsed members are excluded and the others absorb their share.
    /// </summary>
    private double ChainWeight(MagnetChain chain, int member)
    {
        var horizontal = chain.Orientation == MagnetOrientation.Horizontal;
        var sum = 0d;
        var own = 0d;
        var count = chain.Nodes.Count;

        for (var i = 0; i < count; i++)
        {
            if (!_ids.TryGetValue(chain.Nodes[i], out var index) || _nodes[index] is not MagnetView view)
            {
                continue;
            }

            var size = horizontal ? view.WidthSizing : view.HeightSizing;

            if (size.Unit != MagnetSizingUnit.Constraint)
            {
                continue;
            }

            var weight = i < chain.Weights.Count ? chain.Weights[i] : 1;

            if (weight < 0)
            {
                throw new InvalidOperationException($"MagnetChain '{chain.MagnetId}': weights cannot be negative.");
            }

            sum += weight;

            if (i == member)
            {
                own = weight;
            }
        }

        if (own == 0 && sum == 0)
        {
            var self = _ids.TryGetValue(chain.Nodes[member], out var idx) && _nodes[idx] is MagnetView v && (horizontal ? v.WidthSizing : v.HeightSizing).Unit == MagnetSizingUnit.Constraint;

            if (self)
            {
                throw new InvalidOperationException($"MagnetChain '{chain.MagnetId}': total weight is 0.");
            }
        }

        return own;
    }

    /// <summary>
    /// Refreshes the runtime inputs which are not dirty-tracked: bound views, visibility, effective margins.
    /// </summary>
    private void PrepareRuntime()
    {
        var tape = _tape!;
        var values = _values;
        var metas = tape.Nodes;

        for (var i = 0; i < _nodes.Length; i++)
        {
            ref readonly var meta = ref metas[i];
            byte visible = 1;

            if (meta.IsView)
            {
                var view = _bound[i];
                _views[i] = view;
                visible = view is not null && view.Visibility != Visibility.Collapsed ? (byte) 1 : (byte) 0;

                if (visible == 1 && _forcedCollapsed?.Contains(i) == true)
                {
                    // Transition-scoped override: the end state of a deferred Hide is solved as collapsed
                    // while the view is still natively visible (it is fading out).
                    visible = 0;
                }
            }

            _vis[i] = visible;
            values[meta.VisSlot] = visible;
        }

        var margins = tape.Margins;

        for (var i = 0; i < margins.Length; i++)
        {
            ref readonly var m = ref margins[i];

            if (_vis[m.SelfNode] == 0)
            {
                values[m.EffSlot] = 0;
            }
            else if (m.TargetNode >= 0 && _vis[m.TargetNode] == 0)
            {
                values[m.EffSlot] = values[m.GoneSlot];
            }
            else
            {
                values[m.EffSlot] = values[m.MarginSlot];
            }
        }
    }

    /// <summary>
    /// Executes a measure pass: hugs the content, clamped by the finite stage constraints.
    /// </summary>
    public Size Measure(double stageWidth, double stageHeight)
    {
        var tape = _tape ?? throw new InvalidOperationException("Not compiled.");
        PrepareRuntime();
        _deferredMeasured = false;
        var values = _values;
        Array.Clear(_slopes);
        values[MagnetTape.StageWidthArg] = stageWidth;
        values[MagnetTape.StageHeightArg] = stageHeight;

        if (tape.HasFeedback)
        {
            SnapshotFeedback();
        }

        MeasureAxis(tape.X, MagnetTape.StageRight, stageWidth);
        MeasureAxis(tape.Y, MagnetTape.StageBottom, stageHeight);

        if (tape.HasFeedback && FeedbackChanged())
        {
            // One bounded cross-axis iteration (ConstraintLayout semantics).
            MeasureAxis(tape.X, MagnetTape.StageRight, stageWidth);
            MeasureAxis(tape.Y, MagnetTape.StageBottom, stageHeight);
        }

        LastMeasureArgs = new Size(stageWidth, stageHeight);
        LastMeasured = new Size(values[MagnetTape.StageRight], values[MagnetTape.StageBottom]);
        HasMeasured = true;

        return LastMeasured;
    }

    private void MeasureAxis(in AxisPhases phases, int endSlot, double arg)
    {
        var values = _values;
        var slopes = _slopes;

        // Phase 0: independent of the stage end.
        RunNormal(phases.Start, phases.OneStart, MeasurePass.Immediate);

        // Phase 1 (affine in the stage end), then hug resolution.
        values[endSlot] = 0;
        slopes[endSlot] = 1;
        _eval = arg;
        RunAffine(phases.OneStart, phases.ReqStart, MeasurePass.Immediate);
        StageEnd(phases.StageEndOp);
        var linear = true;

        if (phases.HasPiecewise)
        {
            // Second linearization around the hug value: refines piecewise ops (clamps, barriers).
            var first = values[endSlot];
            _eval = first;
            values[endSlot] = 0;
            slopes[endSlot] = 1;
            RunAffine(phases.OneStart, phases.ReqStart, MeasurePass.None);
            StageEnd(phases.StageEndOp);
            linear = Math.Abs(values[endSlot] - first) < 1e-6;
        }

        if (linear)
        {
            // Every phase-1 slot is a + b·W with branches already chosen at W: finalize in one sweep.
            Finalize(phases.OneStart, phases.ReqStart, values[endSlot]);
        }
        else
        {
            RunNormal(phases.OneStart, phases.ReqStart, MeasurePass.None);
        }
    }

    /// <summary>
    /// Replaces the affine (value at W = 0, slope) representation of the phase-1 slots with their value at <paramref name="w" />.
    /// Idempotent per slot (slope is reset to 0), so slots written by more than one op are finalized once.
    /// </summary>
    private void Finalize(int start, int end, double w)
    {
        var ops = _tape!.Ops;
        ref var v0 = ref MemoryMarshal.GetArrayDataReference(_values);
        ref var s0 = ref MemoryMarshal.GetArrayDataReference(_slopes);

        for (var i = start; i < end; i++)
        {
            ref readonly var op = ref ops[i];

            if (op.Kind == OpKind.MeasureChild)
            {
                continue;
            }

            ref var slope = ref Unsafe.Add(ref s0, op.Dst);
            Unsafe.Add(ref v0, op.Dst) += slope * w;
            slope = 0;
        }
    }

    /// <summary>
    /// Executes an arrange pass with the given stage size.
    /// </summary>
    /// <param name="stageWidth">The stage width.</param>
    /// <param name="stageHeight">The stage height.</param>
    /// <param name="measure">Which child measures to run: All for a fresh solve, Deferred when the immediate
    /// measures of the last measure pass are still valid, None during transition frames.</param>
    public void Arrange(double stageWidth, double stageHeight, MeasurePass measure)
    {
        var tape = _tape ?? throw new InvalidOperationException("Not compiled.");

        if (measure != MeasurePass.All && HasMeasured && stageWidth == LastMeasured.Width && stageHeight == LastMeasured.Height)
        {
            // Arranging at the measured size: the slots already hold this exact solution — but deferred measures
            // were skipped by the measure pass and must run once against it.
            if (measure != MeasurePass.None && !_deferredMeasured)
            {
                var deferredOps = tape.DeferredMeasureOps;

                for (var i = 0; i < deferredOps.Length; i++)
                {
                    MeasureChild(in tape.Ops[deferredOps[i]], false);
                }
            }

            return;
        }

        PrepareRuntime();
        var values = _values;
        values[MagnetTape.StageWidthArg] = stageWidth;
        values[MagnetTape.StageHeightArg] = stageHeight;
        values[MagnetTape.StageRight] = stageWidth;
        values[MagnetTape.StageBottom] = stageHeight;
        _slopes[MagnetTape.StageRight] = 0;
        _slopes[MagnetTape.StageBottom] = 0;

        if (tape.HasFeedback)
        {
            SnapshotFeedback();
        }

        RunNormal(tape.X.Start, tape.X.ReqStart, measure);
        RunNormal(tape.Y.Start, tape.Y.ReqStart, measure);

        if (tape.HasFeedback && FeedbackChanged())
        {
            RunNormal(tape.X.Start, tape.X.ReqStart, measure);
            RunNormal(tape.Y.Start, tape.Y.ReqStart, measure);
        }
    }

    /// <summary>
    /// Snapshots the feedback slots (values used by the X pass) — call before executing.
    /// </summary>
    private void SnapshotFeedback()
    {
        var slots = _tape!.FeedbackSlots;

        for (var i = 0; i < slots.Length; i++)
        {
            _feedbackPrev[i] = _values[slots[i]];
        }
    }

    /// <summary>
    /// Whether the Y pass changed any feedback slot with respect to the value the X pass used.
    /// </summary>
    private bool FeedbackChanged()
    {
        var slots = _tape!.FeedbackSlots;

        for (var i = 0; i < slots.Length; i++)
        {
            if (Math.Abs(_values[slots[i]] - _feedbackPrev[i]) > 0.01)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Binds (or unbinds, with <c>null</c>) the view resolved for a node in THIS layout. Bindings survive
    /// recompilation; the compiled projection is refreshed at <see cref="Compile" />.
    /// </summary>
    public void BindView(MagnetView node, IView? view)
    {
        if (view is null)
        {
            _bindings.Remove(node);
        }
        else
        {
            _bindings[node] = view;
        }

        if (_indexOf.TryGetValue(node, out var index))
        {
            _bound[index] = view;
        }
    }

    /// <summary>
    /// Gets the view bound to a node in this layout (independent of compilation).
    /// </summary>
    public IView? GetBoundView(MagnetView node) => _bindings.GetValueOrDefault(node);

    /// <summary>
    /// Gets the compiled index of a node, or -1.
    /// </summary>
    public int IndexOf(MagnetNode node) => _indexOf.GetValueOrDefault(node, -1);

    /// <summary>
    /// Sets (or clears, with <c>null</c>) the transition-scoped set of node indexes solved as collapsed
    /// regardless of the bound view's visibility.
    /// </summary>
    public void SetForcedCollapsed(HashSet<int>? nodes) => _forcedCollapsed = nodes;

    /// <summary>
    /// Gets the frame of a node.
    /// </summary>
    public Rect GetFrame(int node)
    {
        ref readonly var meta = ref _tape!.Nodes[node];
        var values = _values;

        return Rect.FromLTRB(values[meta.Left], values[meta.Top], values[meta.Right], values[meta.Bottom]);
    }

    /// <summary>
    /// Gets whether the node is collapsed (as of the last execution).
    /// </summary>
    public bool IsCollapsed(int node) => _vis[node] == 0;

    /// <summary>
    /// Gets the view bound to a node (as of the last execution).
    /// </summary>
    public IView? GetView(int node) => _views[node];

    /// <summary>
    /// Copies the input slots into <paramref name="destination" />.
    /// </summary>
    public void CopyInputs(double[] destination)
        => Array.Copy(_values, _tape!.InputStart, destination, 0, _tape.InputEnd - _tape.InputStart);

    /// <summary>
    /// Writes lerp(start, end, t) into the input slots.
    /// </summary>
    public void LerpInputs(double[] start, double[] end, double t)
    {
        var tape = _tape!;
        var values = _values;
        var offset = tape.InputStart;
        var count = tape.InputEnd - offset;

        for (var i = 0; i < count; i++)
        {
            var s = start[i];
            values[offset + i] = s + ((end[i] - s) * t);
        }
    }

    /// <summary>
    /// Copies the whole value array into <paramref name="destination" /> (resized when needed).
    /// </summary>
    public void SnapshotValues(ref double[] destination)
    {
        if (destination.Length != _values.Length)
        {
            destination = new double[_values.Length];
        }

        Array.Copy(_values, destination, _values.Length);
    }

    /// <summary>
    /// Restores a value array previously captured with <see cref="SnapshotValues" />.
    /// </summary>
    public void RestoreValues(double[] source) => Array.Copy(source, _values, _values.Length);

    /// <summary>
    /// Number of input slots.
    /// </summary>
    public int InputCount => _tape is { } t ? t.InputEnd - t.InputStart : 0;

    #region Executor

    private void StageEnd(int opIndex)
    {
        ref readonly var op = ref _tape!.Ops[opIndex];
        var values = _values;
        var slopes = _slopes;
        var reqSlots = _tape.ReqSlots;
        var hug = 0d;
        var end = op.A + op.B;

        for (var i = op.A; i < end; i++)
        {
            var entry = reqSlots[i];
            double a, b;

            if (entry >= 0)
            {
                // expr ≥ 0 with expr = a + b·W
                a = values[entry];
                b = slopes[entry];
            }
            else
            {
                // slot ≤ W with slot = a' + b'·W  ⇔  (−a') + (1 − b')·W ≥ 0
                var slot = ~entry;
                a = -values[slot];
                b = 1 - slopes[slot];
            }

            if (b > 1e-9)
            {
                var t = -a / b;

                if (t > hug)
                {
                    hug = t;
                }
            }
        }

        var arg = values[op.C == 0 ? MagnetTape.StageWidthArg : MagnetTape.StageHeightArg];
        var result = double.IsPositiveInfinity(arg) ? hug : Math.Min(hug, arg);

        if (double.IsNaN(result) || result < 0)
        {
            result = 0;
        }

        values[op.Dst] = result;
        slopes[op.Dst] = 0;
    }

    private static bool ShouldMeasure(MeasurePass pass, int dst)
        => pass switch
        {
            MeasurePass.All => true,
            MeasurePass.Immediate => dst == -1,
            MeasurePass.Deferred => dst == -2,
            _ => false
        };

    /// <summary>
    /// Executes ops with concrete values (slopes of written slots are reset).
    /// </summary>
    private void RunNormal(int start, int end, MeasurePass measure)
    {
        var ops = _tape!.Ops;
        var coefficients = _tape.Coefficients;
        ref var v0 = ref MemoryMarshal.GetArrayDataReference(_values);
        ref var s0 = ref MemoryMarshal.GetArrayDataReference(_slopes);

        for (var i = start; i < end; i++)
        {
            ref readonly var op = ref ops[i];

            if (op.Kind == OpKind.MeasureChild)
            {
                if (ShouldMeasure(measure, op.Dst))
                {
                    MeasureChild(in op, false);
                }

                continue;
            }

            Unsafe.Add(ref s0, op.Dst) = 0;

            switch (op.Kind)
            {
                case OpKind.LinComb:
                    Unsafe.Add(ref v0, op.Dst) = (coefficients[op.K1] * Unsafe.Add(ref v0, op.A)) + (coefficients[op.K2] * Unsafe.Add(ref v0, op.B)) + (coefficients[op.K3] * Unsafe.Add(ref v0, op.C));

                    break;

                case OpKind.MulAdd:
                    Unsafe.Add(ref v0, op.Dst) = (Unsafe.Add(ref v0, op.A) * Unsafe.Add(ref v0, op.B)) + Unsafe.Add(ref v0, op.C);

                    break;

                case OpKind.Div:
                {
                    var d = Unsafe.Add(ref v0, op.B);
                    Unsafe.Add(ref v0, op.Dst) = d == 0 ? 0 : Unsafe.Add(ref v0, op.A) / d;

                    break;
                }

                case OpKind.MinRange:
                case OpKind.MaxRange:
                {
                    var isMax = op.Kind == OpKind.MaxRange;
                    var best = Unsafe.Add(ref v0, op.A);
                    var last = op.A + op.B;

                    for (var s = op.A + 1; s < last; s++)
                    {
                        var v = Unsafe.Add(ref v0, s);

                        if (isMax ? v > best : v < best)
                        {
                            best = v;
                        }
                    }

                    Unsafe.Add(ref v0, op.Dst) = double.IsInfinity(best) ? 0 : best;

                    break;
                }

                case OpKind.SumRange:
                {
                    var sum = 0d;
                    var last = op.A + op.B;

                    for (var s = op.A; s < last; s++)
                    {
                        sum += Unsafe.Add(ref v0, s);
                    }

                    Unsafe.Add(ref v0, op.Dst) = sum;

                    break;
                }

                case OpKind.Clamp:
                {
                    var x = Unsafe.Add(ref v0, op.A);
                    var min = Unsafe.Add(ref v0, op.B);
                    var max = Unsafe.Add(ref v0, op.C);
                    Unsafe.Add(ref v0, op.Dst) = x < min ? min : x > max ? max : x;

                    break;
                }

                case OpKind.Gather:
                    Unsafe.Add(ref v0, op.Dst) = _vis[op.A] != 0 ? Unsafe.Add(ref v0, op.B) : coefficients[op.K1];

                    break;
            }
        }
    }

    /// <summary>
    /// Executes ops in affine mode: every slot carries (value at stageEnd = 0, slope w.r.t. stageEnd);
    /// piecewise ops choose their branch at the current evaluation point.
    /// </summary>
    private void RunAffine(int start, int end, MeasurePass measure)
    {
        var ops = _tape!.Ops;
        var coefficients = _tape.Coefficients;
        ref var v0 = ref MemoryMarshal.GetArrayDataReference(_values);
        ref var s0 = ref MemoryMarshal.GetArrayDataReference(_slopes);

        for (var i = start; i < end; i++)
        {
            ref readonly var op = ref ops[i];

            switch (op.Kind)
            {
                case OpKind.LinComb:
                {
                    var k1 = coefficients[op.K1];
                    var k2 = coefficients[op.K2];
                    var k3 = coefficients[op.K3];
                    Unsafe.Add(ref v0, op.Dst) = (k1 * Unsafe.Add(ref v0, op.A)) + (k2 * Unsafe.Add(ref v0, op.B)) + (k3 * Unsafe.Add(ref v0, op.C));
                    Unsafe.Add(ref s0, op.Dst) = (k1 * Unsafe.Add(ref s0, op.A)) + (k2 * Unsafe.Add(ref s0, op.B)) + (k3 * Unsafe.Add(ref s0, op.C));

                    break;
                }

                case OpKind.MulAdd:
                {
                    var scalar = Unsafe.Add(ref v0, op.B);
                    Unsafe.Add(ref v0, op.Dst) = (Unsafe.Add(ref v0, op.A) * scalar) + Unsafe.Add(ref v0, op.C);
                    Unsafe.Add(ref s0, op.Dst) = (Unsafe.Add(ref s0, op.A) * scalar) + Unsafe.Add(ref s0, op.C);

                    break;
                }

                case OpKind.Div:
                {
                    var d = Unsafe.Add(ref v0, op.B);
                    Unsafe.Add(ref v0, op.Dst) = d == 0 ? 0 : Unsafe.Add(ref v0, op.A) / d;
                    Unsafe.Add(ref s0, op.Dst) = d == 0 ? 0 : Unsafe.Add(ref s0, op.A) / d;

                    break;
                }

                case OpKind.MinRange:
                case OpKind.MaxRange:
                {
                    var isMax = op.Kind == OpKind.MaxRange;
                    var best = op.A;
                    var last = op.A + op.B;

                    for (var s = op.A + 1; s < last; s++)
                    {
                        if (isMax ? AffineGreater(s, best) : AffineGreater(best, s))
                        {
                            best = s;
                        }
                    }

                    var v = Unsafe.Add(ref v0, best);

                    if (double.IsInfinity(v))
                    {
                        Unsafe.Add(ref v0, op.Dst) = 0;
                        Unsafe.Add(ref s0, op.Dst) = 0;
                    }
                    else
                    {
                        Unsafe.Add(ref v0, op.Dst) = v;
                        Unsafe.Add(ref s0, op.Dst) = Unsafe.Add(ref s0, best);
                    }

                    break;
                }

                case OpKind.SumRange:
                {
                    var sum = 0d;
                    var slope = 0d;
                    var last = op.A + op.B;

                    for (var s = op.A; s < last; s++)
                    {
                        sum += Unsafe.Add(ref v0, s);
                        slope += Unsafe.Add(ref s0, s);
                    }

                    Unsafe.Add(ref v0, op.Dst) = sum;
                    Unsafe.Add(ref s0, op.Dst) = slope;

                    break;
                }

                case OpKind.Clamp:
                {
                    var min = Unsafe.Add(ref v0, op.B);
                    var max = Unsafe.Add(ref v0, op.C);
                    var x = Eval(op.A);

                    if (!double.IsNegativeInfinity(min) && x < min)
                    {
                        Unsafe.Add(ref v0, op.Dst) = min;
                        Unsafe.Add(ref s0, op.Dst) = Unsafe.Add(ref s0, op.B);
                    }
                    else if (!double.IsPositiveInfinity(max) && x > max)
                    {
                        Unsafe.Add(ref v0, op.Dst) = max;
                        Unsafe.Add(ref s0, op.Dst) = Unsafe.Add(ref s0, op.C);
                    }
                    else
                    {
                        Unsafe.Add(ref v0, op.Dst) = Unsafe.Add(ref v0, op.A);
                        Unsafe.Add(ref s0, op.Dst) = Unsafe.Add(ref s0, op.A);
                    }

                    break;
                }

                case OpKind.Gather:
                    if (_vis[op.A] != 0)
                    {
                        Unsafe.Add(ref v0, op.Dst) = Unsafe.Add(ref v0, op.B);
                        Unsafe.Add(ref s0, op.Dst) = Unsafe.Add(ref s0, op.B);
                    }
                    else
                    {
                        Unsafe.Add(ref v0, op.Dst) = coefficients[op.K1];
                        Unsafe.Add(ref s0, op.Dst) = 0;
                    }

                    break;

                case OpKind.MeasureChild:
                    if (ShouldMeasure(measure, op.Dst))
                    {
                        MeasureChild(in op, true);
                    }

                    break;
            }
        }
    }

    private void MeasureChild(in Op op, bool affine)
    {
        if (op.Dst == -2)
        {
            _deferredMeasured = true;
        }

        ref readonly var meta = ref _tape!.Nodes[op.A];
        var values = _values;
        var view = _views[op.A];
        var width = 0d;
        var height = 0d;

        if (view is not null && _vis[op.A] != 0)
        {
            var wc = affine ? Eval(op.B) : values[op.B];
            var hc = affine ? Eval(op.C) : values[op.C];

            if (double.IsNaN(wc))
            {
                wc = double.PositiveInfinity;
            }
            else if (wc < 0)
            {
                wc = 0;
            }

            if (double.IsNaN(hc))
            {
                hc = double.PositiveInfinity;
            }
            else if (hc < 0)
            {
                hc = 0;
            }

            var size = view.Measure(wc, hc);
            width = size.Width;
            height = size.Height;
        }

        // Views with no Measured axis are measured too (the MAUI contract: containers size their own
        // content from the measure pass) but have no measured slots to write.
        if (meta.MeasuredWidth >= 0)
        {
            values[meta.MeasuredWidth] = width;
            values[meta.MeasuredHeight] = height;
            _slopes[meta.MeasuredWidth] = 0;
            _slopes[meta.MeasuredHeight] = 0;
        }
    }

    /// <summary>
    /// Evaluates an affine slot at the current evaluation point of the stage end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Eval(int slot)
    {
        var v = _values[slot];

        if (double.IsInfinity(v))
        {
            // Raw infinite constraint (unbounded stage argument): dominates any slope.
            return v;
        }

        var b = _slopes[slot];

        if (double.IsPositiveInfinity(_eval))
        {
            return b > 0 ? double.PositiveInfinity : b < 0 ? double.NegativeInfinity : v;
        }

        return v + (b * _eval);
    }

    /// <summary>
    /// Compares two affine slots at the current evaluation point (lexicographic on slope when unbounded).
    /// </summary>
    private bool AffineGreater(int a, int b)
    {
        if (double.IsPositiveInfinity(_eval) && !double.IsInfinity(_values[a]) && !double.IsInfinity(_values[b]))
        {
            var sa = _slopes[a];
            var sb = _slopes[b];

            return sa != sb ? sa > sb : _values[a] > _values[b];
        }

        return Eval(a) > Eval(b);
    }

    #endregion
}
