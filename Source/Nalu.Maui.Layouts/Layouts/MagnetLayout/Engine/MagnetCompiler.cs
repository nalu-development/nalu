using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Nalu.MagnetLayout.Engine;

/// <summary>
/// Compiles a set of <see cref="MagnetNode" />s into a <see cref="MagnetTape" />.
/// Pure: the produced tape references neither the nodes nor the layout.
/// </summary>
internal sealed class MagnetCompiler
{
    private enum NodeKind : byte
    {
        View,
        Barrier,
        Guideline,
        Chain
    }

    private sealed class ResolvedAnchor
    {
        public int Target; // -1 = parent
        public MagnetPole Pole;
        public int MarginSlot;
        public int GoneSlot;
        public int EffSlot;
        public bool Adjacent; // chain-internal anchor to the neighbour: contributes its margin only
    }

    private sealed class ViewAxis
    {
        public ResolvedAnchor? Start;
        public ResolvedAnchor? End;
        public MagnetSizing Size;
        public int ValueSlot, MinSlot, MaxSlot, BiasSlot;
        public int SizeSlot = -1; // final size slot (assigned during emission or by the chain)
        public int Chain = -1;
        public bool Weighted;
        public int WeightedSizeSlot = -1;
    }

    private sealed class NodeInfo
    {
        public required MagnetNode Node;
        public required string Id;
        public NodeKind Kind;
        public int VisSlot;
        public int[] Pole = new int[4]; // Left, Right, Top, Bottom slots
        public int MeasuredWidth = -1, MeasuredHeight = -1;
        public ViewAxis[] Axes = [new ViewAxis(), new ViewAxis()];
        public bool Measured;

        // virtual nodes
        public int Axis; // 0 = X, 1 = Y
        public int[] Members = [];
        public int MarginSlot, PercentSlot, PositionSlot;
        public int[] WeightSlots = [];
        public int StartSlot = -1, EndSlot = -1, SpanSlot = -1; // chain
        public int RatioFeedbackSlot = -1; // Ratio width fed by a Y-dependent height
    }

    private readonly NodeInfo[] _nodes;
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly List<Op> _ops;
    private readonly List<PatchEntry> _patches;
    private readonly List<MarginEntry> _margins;
    private readonly List<int> _reqSlots;
    private readonly List<int> _feedbackSlots = [];
    private int _slots;
    private int _inputStart, _inputEnd;

    private MagnetCompiler(IReadOnlyList<MagnetNode> nodes)
    {
        _nodes = new NodeInfo[nodes.Count];
        _ops = new List<Op>(nodes.Count * 14);
        _patches = new List<PatchEntry>(nodes.Count * 12);
        _margins = new List<MarginEntry>(nodes.Count * 2);
        _reqSlots = new List<int>(nodes.Count * 3);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var id = node.MagnetId;

            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException($"Every MagnetNode requires a MagnetId ({node.GetType().Name} at position {i}).");
            }

            if (!_index.TryAdd(id, i))
            {
                throw new InvalidOperationException($"MagnetId '{id}' is defined more than once.");
            }

            _nodes[i] = new NodeInfo
            {
                Node = node,
                Id = id,
                Kind = node switch
                {
                    MagnetView => NodeKind.View,
                    MagnetBarrier => NodeKind.Barrier,
                    MagnetGuideline => NodeKind.Guideline,
                    MagnetChain => NodeKind.Chain,
                    _ => throw new NotSupportedException($"Unsupported node type {node.GetType().Name}.")
                }
            };
            node.Index = i;
        }
    }

    public static MagnetTape Compile(IReadOnlyList<MagnetNode> nodes) => new MagnetCompiler(nodes).Run();

    /// <summary>
    /// Returns a tape for the given nodes, reusing a cached one when a structurally identical set was compiled before
    /// (typical of template-instantiated cells). Tapes are pure, so sharing them across layouts is safe.
    /// </summary>
    public static MagnetTape GetOrCompile(IReadOnlyList<MagnetNode> nodes)
    {
        var key = MagnetTapeCache.CreateKey(nodes);

        if (MagnetTapeCache.TryGet(key, out var cached))
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].Index = i;
            }

            return cached;
        }

        var tape = Compile(nodes);
        MagnetTapeCache.Add(key, tape);

        return tape;
    }

    private MagnetTape Run()
    {
        _slots = MagnetTape.FixedSlots;

        // Visibility slots (runtime inputs)
        foreach (var n in _nodes)
        {
            n.VisSlot = Alloc();
        }

        // Patchable inputs: contiguous
        _inputStart = _slots;
        AllocateInputs();
        _inputEnd = _slots;

        // Measured & pole slots
        foreach (var n in _nodes)
        {
            if (n.Kind == NodeKind.View)
            {
                var v = (MagnetView) n.Node;
                n.Measured = v.WidthSizing.Unit == MagnetSizingUnit.Measured || v.HeightSizing.Unit == MagnetSizingUnit.Measured;

                if (n.Measured)
                {
                    n.MeasuredWidth = Alloc();
                    n.MeasuredHeight = Alloc();
                }

                n.Pole[0] = Alloc();
                n.Pole[1] = Alloc();
                n.Pole[2] = Alloc();
                n.Pole[3] = Alloc();
            }
            else
            {
                var slot = Alloc();

                if (n.Axis == 0)
                {
                    n.Pole[0] = n.Pole[1] = slot;
                    n.Pole[2] = n.Pole[3] = -1;
                }
                else
                {
                    n.Pole[2] = n.Pole[3] = slot;
                    n.Pole[0] = n.Pole[1] = -1;
                }
            }
        }

        ResolveAnchors();
        ResolveChains();
        ResolveBarriers();

        var x = EmitAxis(0);
        var y = EmitAxis(1);

        // Safety net: the executor accesses slots without bounds checks, so every index must be valid here.
        foreach (var op in _ops)
        {
            var dstOk = op.Kind == OpKind.MeasureChild ? op.Dst == -1 : (uint) op.Dst < (uint) _slots;
            var rangeOp = op.Kind is OpKind.MinRange or OpKind.MaxRange or OpKind.SumRange;
            var aOk = op.Kind is OpKind.MeasureChild or OpKind.Gather ? (uint) op.A < (uint) _nodes.Length : op.Kind == OpKind.StageEnd || (uint) op.A < (uint) _slots;
            var bOk = rangeOp ? op.B >= 0 && op.A + op.B <= _slots : op.Kind == OpKind.StageEnd || (uint) op.B < (uint) _slots;
            var cOk = op.Kind == OpKind.StageEnd || (uint) op.C < (uint) _slots;

            if (!dstOk || !aOk || !bOk || !cOk)
            {
                throw new InvalidOperationException($"Magnet compiler emitted an invalid instruction: {op.ToString(_coefficients.ToArray())}");
            }
        }

        var meta = new NodeMeta[_nodes.Length];

        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            meta[i] = new NodeMeta
            {
                IsView = n.Kind == NodeKind.View,
                VisSlot = n.VisSlot,
                Left = n.Pole[0],
                Right = n.Pole[1],
                Top = n.Pole[2],
                Bottom = n.Pole[3],
                MeasuredWidth = n.MeasuredWidth,
                MeasuredHeight = n.MeasuredHeight
            };
        }

        return new MagnetTape
        {
            Ops = _ops.ToArray(),
            Coefficients = _coefficients.ToArray(),
            ValueCount = _slots,
            ReqSlots = _reqSlots.ToArray(),
            X = x,
            Y = y,
            Nodes = meta,
            Patches = _patches.ToArray(),
            Margins = _margins.ToArray(),
            InputStart = _inputStart,
            InputEnd = _inputEnd,
            FeedbackSlots = _feedbackSlots.ToArray()
        };
    }

    #region Slots & inputs

    private int Alloc() => _slots++;

    private readonly List<double> _coefficients = [0, 1, -1];

    private byte Coef(double value)
    {
        var index = _coefficients.IndexOf(value);

        if (index < 0)
        {
            index = _coefficients.Count;

            if (index > byte.MaxValue)
            {
                throw new InvalidOperationException("Too many distinct structural coefficients.");
            }

            _coefficients.Add(value);
        }

        return (byte) index;
    }

    private int Input(int node, PatchKind kind, int aux)
    {
        var slot = Alloc();
        _patches.Add(new PatchEntry(node, kind, aux, slot));

        return slot;
    }

    private void AllocateInputs()
    {
        // Chain members need a bias slot on the chain axis (packed style) even without both anchors.
        var chainMember = new bool[_nodes.Length * 2];

        foreach (var n in _nodes)
        {
            if (n.Node is MagnetChain chain)
            {
                var axis = chain.Orientation == MagnetOrientation.Horizontal ? 0 : 1;

                foreach (var id in chain.Nodes)
                {
                    if (id is not null && _index.TryGetValue(id, out var m))
                    {
                        chainMember[(m * 2) + axis] = true;
                    }
                }
            }
        }

        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            switch (n.Node)
            {
                case MagnetView view:
                    for (var axis = 0; axis < 2; axis++)
                    {
                        var ax = n.Axes[axis];
                        ax.Size = axis == 0 ? view.WidthSizing : view.HeightSizing;
                        ax.ValueSlot = Input(i, PatchKind.SizeValue, axis);

                        if (ax.Size.HasBounds)
                        {
                            ax.MinSlot = Input(i, PatchKind.SizeMin, axis);
                            ax.MaxSlot = Input(i, PatchKind.SizeMax, axis);
                        }
                        else
                        {
                            ax.MinSlot = MagnetTape.Zero;
                            ax.MaxSlot = MagnetTape.PosInf;
                        }

                        ax.Start = AllocateAnchor(i, axis == 0 ? view.LeftTo : view.TopTo, axis == 0 ? 0 : 2);
                        ax.End = AllocateAnchor(i, axis == 0 ? view.RightTo : view.BottomTo, axis == 0 ? 1 : 3);
                        ax.BiasSlot = (ax.Start is not null && ax.End is not null) || chainMember[(i * 2) + axis]
                            ? Input(i, axis == 0 ? PatchKind.BiasX : PatchKind.BiasY, 0)
                            : MagnetTape.Zero;
                    }

                    break;

                case MagnetBarrier barrier:
                    n.Axis = barrier.Direction is MagnetPole.Left or MagnetPole.Right ? 0 : 1;
                    n.MarginSlot = Input(i, PatchKind.BarrierMargin, 0);

                    break;

                case MagnetGuideline guideline:
                    n.Axis = guideline.Orientation == MagnetOrientation.Vertical ? 0 : 1;
                    n.PercentSlot = Input(i, PatchKind.GuidelinePercent, 0);
                    n.PositionSlot = Input(i, PatchKind.GuidelinePosition, 0);

                    break;

                case MagnetChain chain:
                    n.Axis = chain.Orientation == MagnetOrientation.Horizontal ? 0 : 1;
                    n.WeightSlots = new int[chain.Nodes.Count];

                    for (var m = 0; m < chain.Nodes.Count; m++)
                    {
                        n.WeightSlots[m] = Input(i, PatchKind.ChainWeight, m);
                    }

                    break;
            }
        }
    }

    private ResolvedAnchor? AllocateAnchor(int node, MagnetAnchor? anchor, int side)
    {
        if (anchor is not { } a)
        {
            return null;
        }

        return new ResolvedAnchor
        {
            Pole = a.Pole,
            MarginSlot = Input(node, PatchKind.AnchorMargin, side),
            GoneSlot = Input(node, PatchKind.AnchorGoneMargin, side)
        };
    }

    #endregion

    #region Resolution

    private void ResolveAnchors()
    {
        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            if (n.Kind != NodeKind.View)
            {
                continue;
            }

            var view = (MagnetView) n.Node;
            Resolve(i, n.Axes[0].Start, view.LeftTo, nameof(MagnetView.LeftTo), 0);
            Resolve(i, n.Axes[0].End, view.RightTo, nameof(MagnetView.RightTo), 0);
            Resolve(i, n.Axes[1].Start, view.TopTo, nameof(MagnetView.TopTo), 1);
            Resolve(i, n.Axes[1].End, view.BottomTo, nameof(MagnetView.BottomTo), 1);
        }
    }

    private void Resolve(int node, ResolvedAnchor? resolved, MagnetAnchor? anchor, string property, int axis)
    {
        if (resolved is null || anchor is not { } a)
        {
            return;
        }

        var n = _nodes[node];
        var poleAxis = a.Pole is MagnetPole.Left or MagnetPole.Right ? 0 : 1;

        if (poleAxis != axis)
        {
            throw new InvalidOperationException($"MagnetView '{n.Id}': {property} cannot reference pole '{a.Pole}' of '{a.Target}'.");
        }

        if (a.TargetsParent)
        {
            resolved.Target = -1;
        }
        else
        {
            if (!_index.TryGetValue(a.Target, out var target))
            {
                throw new InvalidOperationException($"MagnetView '{n.Id}': {property} targets unknown id '{a.Target}'.");
            }

            if (target == node)
            {
                throw new InvalidOperationException($"MagnetView '{n.Id}': {property} cannot target itself.");
            }

            var t = _nodes[target];

            if (t.Kind == NodeKind.Chain)
            {
                throw new InvalidOperationException($"MagnetView '{n.Id}': {property} cannot target the MagnetChain '{t.Id}'; anchor to its first/last member instead.");
            }

            if (t.Kind != NodeKind.View && t.Axis != axis)
            {
                throw new InvalidOperationException(
                    $"MagnetView '{n.Id}': {property} cannot reference pole '{a.Pole}' of '{t.Id}' ({(axis == 0 ? "a horizontal" : "a vertical")} node)."
                );
            }

            resolved.Target = target;
        }

        resolved.EffSlot = Alloc();
        _margins.Add(new MarginEntry(node, resolved.Target, resolved.MarginSlot, resolved.GoneSlot, resolved.EffSlot));
    }

    private void ResolveChains()
    {
        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            if (n.Kind != NodeKind.Chain)
            {
                continue;
            }

            var chain = (MagnetChain) n.Node;
            var axis = n.Axis;
            var count = chain.Nodes.Count;

            if (count == 0)
            {
                throw new InvalidOperationException($"MagnetChain '{n.Id}' has no members.");
            }

            n.Members = new int[count];

            for (var m = 0; m < count; m++)
            {
                var id = chain.Nodes[m];

                if (!_index.TryGetValue(id, out var member))
                {
                    throw new InvalidOperationException($"MagnetChain '{n.Id}': member '{id}' is not a known MagnetId.");
                }

                var mn = _nodes[member];

                if (mn.Kind != NodeKind.View)
                {
                    throw new InvalidOperationException($"MagnetChain '{n.Id}': member '{id}' is not a MagnetView.");
                }

                var ax = mn.Axes[axis];

                if (ax.Chain >= 0)
                {
                    throw new InvalidOperationException($"MagnetView '{id}' belongs to more than one {(axis == 0 ? "horizontal" : "vertical")} chain ('{_nodes[ax.Chain].Id}' and '{n.Id}').");
                }

                ax.Chain = i;
                ax.Weighted = ax.Size.Unit == MagnetSizingUnit.Constraint;
                n.Members[m] = member;
            }

            // Validate member anchors along the chain axis.
            for (var m = 0; m < count; m++)
            {
                var member = n.Members[m];
                var ax = _nodes[member].Axes[axis];
                var startPole = axis == 0 ? MagnetPole.Left : MagnetPole.Top;
                var endPole = axis == 0 ? MagnetPole.Right : MagnetPole.Bottom;

                if (ax.Start is { } start && m > 0)
                {
                    if (start.Target == n.Members[m - 1] && start.Pole == endPole)
                    {
                        start.Adjacent = true;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"MagnetView '{_nodes[member].Id}' is a member of MagnetChain '{n.Id}': its {(axis == 0 ? "LeftTo" : "TopTo")} must be empty or point to the previous member '{_nodes[n.Members[m - 1]].Id}'."
                        );
                    }
                }

                if (ax.End is { } end && m < count - 1)
                {
                    if (end.Target == n.Members[m + 1] && end.Pole == startPole)
                    {
                        end.Adjacent = true;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"MagnetView '{_nodes[member].Id}' is a member of MagnetChain '{n.Id}': its {(axis == 0 ? "RightTo" : "BottomTo")} must be empty or point to the next member '{_nodes[n.Members[m + 1]].Id}'."
                        );
                    }
                }
            }
        }
    }

    private void ResolveBarriers()
    {
        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            if (n.Kind != NodeKind.Barrier)
            {
                continue;
            }

            var barrier = (MagnetBarrier) n.Node;
            n.Members = new int[barrier.Nodes.Count];

            for (var m = 0; m < barrier.Nodes.Count; m++)
            {
                var id = barrier.Nodes[m];

                if (!_index.TryGetValue(id, out var member))
                {
                    throw new InvalidOperationException($"MagnetBarrier '{n.Id}': member '{id}' is not a known MagnetId.");
                }

                var mn = _nodes[member];

                if (mn.Kind == NodeKind.Chain || (mn.Kind != NodeKind.View && mn.Axis != n.Axis))
                {
                    throw new InvalidOperationException($"MagnetBarrier '{n.Id}': member '{id}' has no {(n.Axis == 0 ? "horizontal" : "vertical")} poles.");
                }

                n.Members[m] = member;
            }
        }
    }

    #endregion

    #region Graph

    // Vertex ids per axis: node n → 2n (Size / ChainSpan), 2n+1 (Pos / Chain / Barrier / Guideline); stage end → 2N.
    private int SizeV(int node) => node * 2;
    private int PosV(int node) => (node * 2) + 1;
    private int StageV => _nodes.Length * 2;

    /// <summary>
    /// Compressed sparse dependency graph: vertex v depends on To[Start[v]..Start[v+1]).
    /// </summary>
    private readonly struct Graph(int[] start, int[] to)
    {
        public readonly int[] Start = start;
        public readonly int[] To = to;
        public int Count => Start.Length - 1;
    }

    private Graph BuildGraph(int axis)
    {
        var count = (_nodes.Length * 2) + 1;
        var edges = _edges;
        edges.Clear();

        void Dep(int vertex, int on) => edges.Add(((long) vertex << 32) | (uint) on);

        void AnchorDep(int vertex, ResolvedAnchor? anchor)
        {
            if (anchor is null || anchor.Adjacent)
            {
                return;
            }

            if (anchor.Target < 0)
            {
                if (anchor.Pole is MagnetPole.Right or MagnetPole.Bottom)
                {
                    Dep(vertex, StageV);
                }
            }
            else
            {
                Dep(vertex, PosV(anchor.Target));
            }
        }

        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            switch (n.Kind)
            {
                case NodeKind.View:
                {
                    var ax = n.Axes[axis];
                    var size = SizeV(i);
                    var pos = PosV(i);
                    Dep(pos, size);

                    if (ax.Size.Unit == MagnetSizingUnit.StagePercent)
                    {
                        Dep(size, StageV);
                    }

                    if (ax.Chain >= 0)
                    {
                        Dep(pos, PosV(ax.Chain));

                        if (ax.Weighted)
                        {
                            Dep(size, PosV(ax.Chain));
                        }
                        else if (ax.Size.Unit is MagnetSizingUnit.Measured or MagnetSizingUnit.ConstraintPercent || (axis == 0 && n.Measured))
                        {
                            Dep(size, SizeV(ax.Chain));

                            if (ax.Size.Unit == MagnetSizingUnit.Measured)
                            {
                                foreach (var m in ChainSiblingsToLeaveRoomFor(ax.Chain, i, axis))
                                {
                                    Dep(size, SizeV(m));
                                }
                            }
                        }
                    }
                    else
                    {
                        AnchorDep(pos, ax.Start);
                        AnchorDep(pos, ax.End);

                        var needsSpan = ax.Size.Unit is MagnetSizingUnit.Constraint or MagnetSizingUnit.ConstraintPercent or MagnetSizingUnit.Measured or MagnetSizingUnit.Ratio
                                        || (axis == 0 && n.Measured);

                        if (needsSpan)
                        {
                            AnchorDep(size, ax.Start);
                            AnchorDep(size, ax.End);
                        }
                    }

                    break;
                }

                case NodeKind.Chain when n.Axis == axis:
                {
                    var span = SizeV(i);
                    var chain = PosV(i);
                    Dep(chain, span);
                    var first = _nodes[n.Members[0]].Axes[axis];
                    var last = _nodes[n.Members[^1]].Axes[axis];

                    if (first.Start is { } s)
                    {
                        AnchorDep(span, s);
                    }

                    if (last.End is { } e)
                    {
                        AnchorDep(span, e);
                    }
                    else
                    {
                        Dep(span, StageV);
                    }

                    foreach (var m in n.Members)
                    {
                        if (!_nodes[m].Axes[axis].Weighted)
                        {
                            Dep(chain, SizeV(m));
                        }
                    }

                    break;
                }

                case NodeKind.Barrier when n.Axis == axis:
                    foreach (var m in n.Members)
                    {
                        Dep(PosV(i), PosV(m));
                    }

                    break;

                case NodeKind.Guideline when n.Axis == axis:
                    Dep(PosV(i), StageV);

                    break;
            }
        }

        return ToCsr(edges, count, fromHigh: true);
    }

    private static Graph ToCsr(List<long> edges, int count, bool fromHigh)
    {
        var start = new int[count + 1];

        foreach (var e in edges)
        {
            var from = fromHigh ? (int) (e >> 32) : (int) e;
            start[from + 1]++;
        }

        for (var i = 0; i < count; i++)
        {
            start[i + 1] += start[i];
        }

        var to = new int[edges.Count];
        var fill = new int[count];

        foreach (var e in edges)
        {
            var from = fromHigh ? (int) (e >> 32) : (int) e;
            var target = fromHigh ? (int) e : (int) (e >> 32);
            to[start[from] + fill[from]++] = target;
        }

        return new Graph(start, to);
    }

    private readonly List<long> _edges = new(64);

    private (int[] order, bool[] phaseOne) SortGraph(int axis, Graph deps)
    {
        var count = deps.Count;
        var color = new byte[count];
        var order = new int[count];
        var orderCount = 0;
        var stack = new List<int>(count);

        void Visit(int v)
        {
            if (color[v] == 2)
            {
                return;
            }

            if (color[v] == 1)
            {
                throw Cycle(axis, stack, v);
            }

            color[v] = 1;
            stack.Add(v);
            var end = deps.Start[v + 1];

            for (var i = deps.Start[v]; i < end; i++)
            {
                Visit(deps.To[i]);
            }

            stack.RemoveAt(stack.Count - 1);
            color[v] = 2;
            order[orderCount++] = v;
        }

        for (var v = 0; v < count; v++)
        {
            Visit(v);
        }

        // Reachability from the stage end vertex through forward edges (same edge list, reversed).
        var forward = ToCsr(_edges, count, fromHigh: false);
        var phaseOne = new bool[count];
        var queue = new int[count];
        var head = 0;
        var tail = 0;
        phaseOne[StageV] = true;
        queue[tail++] = StageV;

        while (head < tail)
        {
            var v = queue[head++];
            var end = forward.Start[v + 1];

            for (var i = forward.Start[v]; i < end; i++)
            {
                var w = forward.To[i];

                if (!phaseOne[w])
                {
                    phaseOne[w] = true;
                    queue[tail++] = w;
                }
            }
        }

        return (order, phaseOne);
    }

    private InvalidOperationException Cycle(int axis, List<int> stack, int v)
    {
        var start = stack.IndexOf(v);
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        var ids = new List<string>();

        for (var i = start; i < stack.Count; i++)
        {
            var id = stack[i] == StageV ? MagnetAnchor.Parent : _nodes[stack[i] / 2].Id;

            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }

        ids.Add(ids[0]);
        sb.Append(FormatCyclePath(ids));

        return new InvalidOperationException($"Constraint cycle on {(axis == 0 ? "X" : "Y")} axis: {sb}. Use a MagnetChain for mutually dependent views.");
    }

    private static string FormatCyclePath(List<string> ids) => string.Join(" → ", ids.Select(i => $"'{i}'"));

    #endregion

    #region Emission

    private int StageStartSlot(int axis) => axis == 0 ? MagnetTape.StageLeft : MagnetTape.StageTop;
    private int StageEndSlot(int axis) => axis == 0 ? MagnetTape.StageRight : MagnetTape.StageBottom;
    private int StageArgSlot(int axis) => axis == 0 ? MagnetTape.StageWidthArg : MagnetTape.StageHeightArg;

    private int PoleSlot(int axis, ResolvedAnchor anchor)
    {
        if (anchor.Target < 0)
        {
            return anchor.Pole is MagnetPole.Left or MagnetPole.Top ? StageStartSlot(axis) : StageEndSlot(axis);
        }

        return _nodes[anchor.Target].Pole[(int) anchor.Pole];
    }

    private void Emit(Op op) => _ops.Add(op);

    private int Lin(int a, double ka, int b = MagnetTape.Zero, double kb = 0, int c = MagnetTape.Zero, double kc = 0)
    {
        var dst = Alloc();
        Emit(new Op(OpKind.LinComb, dst, a, b, c, Coef(ka), Coef(kb), Coef(kc)));

        return dst;
    }

    private void LinInto(int dst, int a, double ka, int b = MagnetTape.Zero, double kb = 0, int c = MagnetTape.Zero, double kc = 0)
        => Emit(new Op(OpKind.LinComb, dst, a, b, c, Coef(ka), Coef(kb), Coef(kc)));

    private int MulAdd(int a, int scalar, int c = MagnetTape.Zero)
    {
        var dst = Alloc();
        Emit(new Op(OpKind.MulAdd, dst, a, scalar, c));

        return dst;
    }

    private void MulAddInto(int dst, int a, int scalar, int c = MagnetTape.Zero) => Emit(new Op(OpKind.MulAdd, dst, a, scalar, c));

    private int Clamp(int a, int min, int max)
    {
        var dst = Alloc();
        Emit(new Op(OpKind.Clamp, dst, a, min, max));

        return dst;
    }

    private int Div(int a, int b)
    {
        var dst = Alloc();
        Emit(new Op(OpKind.Div, dst, a, b, MagnetTape.Zero));

        return dst;
    }

    private AxisPhases EmitAxis(int axis)
    {
        var deps = BuildGraph(axis);
        var (order, phaseOne) = SortGraph(axis, deps);

        var phases = new AxisPhases { Start = _ops.Count };

        // Phase 0
        foreach (var v in order)
        {
            if (!phaseOne[v] && v != StageV)
            {
                EmitVertex(axis, v);
            }
        }

        phases.OneStart = _ops.Count;

        foreach (var v in order)
        {
            if (phaseOne[v] && v != StageV)
            {
                EmitVertex(axis, v);
            }
        }

        for (var i = phases.OneStart; i < _ops.Count; i++)
        {
            var kind = _ops[i].Kind;
            phases.HasPiecewise |= kind is OpKind.Clamp or OpKind.MinRange or OpKind.MaxRange or OpKind.Gather;
        }

        // Requirements: every view must fit within [stageStart, stageEnd].
        phases.ReqStart = _ops.Count;
        var reqStart = _reqSlots.Count;
        var stageEnd = StageEndSlot(axis);

        for (var i = 0; i < _nodes.Length; i++)
        {
            var n = _nodes[i];

            if (n.Kind != NodeKind.View)
            {
                continue;
            }

            var endPole = n.Pole[axis == 0 ? 1 : 3];
            var startPole = n.Pole[axis == 0 ? 0 : 2];
            _reqSlots.Add(~endPole); // end ≤ stageEnd (encoded as the bitwise complement, no op needed)
            _reqSlots.Add(startPole); // start ≥ 0
        }

        _reqSlots.AddRange(_pendingReqs);
        _pendingReqs.Clear();

        phases.StageEndOp = _ops.Count;
        Emit(new Op(OpKind.StageEnd, stageEnd, reqStart, _reqSlots.Count - reqStart, axis));
        phases.End = _ops.Count;

        return phases;
    }

    private readonly List<int> _pendingReqs = [];

    private void EmitVertex(int axis, int v)
    {
        var node = v / 2;
        var n = _nodes[node];
        var isPos = (v & 1) == 1;

        switch (n.Kind)
        {
            case NodeKind.View:
                if (isPos)
                {
                    EmitViewPosition(axis, node);
                }
                else
                {
                    EmitViewSize(axis, node);
                }

                break;

            case NodeKind.Chain when n.Axis == axis:
                if (isPos)
                {
                    EmitChain(axis, node);
                }
                else
                {
                    EmitChainSpan(axis, node);
                }

                break;

            case NodeKind.Barrier when n.Axis == axis && isPos:
                EmitBarrier(axis, node);

                break;

            case NodeKind.Guideline when n.Axis == axis && isPos:
                EmitGuideline(axis, node);

                break;
        }
    }

    /// <summary>
    /// The chain members whose size a Measured member must leave room for: every non-weighted member except the
    /// Measured ones that come after it (they get what is left, in chain order).
    /// </summary>
    private IEnumerable<int> ChainSiblingsToLeaveRoomFor(int chain, int member, int axis)
    {
        var members = _nodes[chain].Members;
        var seenSelf = false;

        foreach (var m in members)
        {
            if (m == member)
            {
                seenSelf = true;

                continue;
            }

            var ax = _nodes[m].Axes[axis];

            if (ax.Weighted || (seenSelf && ax.Size.Unit == MagnetSizingUnit.Measured))
            {
                continue;
            }

            yield return m;
        }
    }

    /// <summary>
    /// Emits ops computing the room available to a Measured chain member: span - sibling sizes - gaps.
    /// </summary>
    private int EmitChainMemberAvail(int chain, int member, int axis)
    {
        var chainInfo = _nodes[chain];
        var avail = chainInfo.SpanSlot;

        foreach (var m in ChainSiblingsToLeaveRoomFor(chain, member, axis))
        {
            avail = Lin(avail, 1, _nodes[m].Axes[axis].SizeSlot, -1);
        }

        // Gaps between adjacent members (margins of adjacent anchors).
        var members = chainInfo.Members;

        for (var i = 0; i < members.Length; i++)
        {
            var ax = _nodes[members[i]].Axes[axis];

            if (i > 0 && ax.Start is { Adjacent: true } start)
            {
                avail = Lin(avail, 1, start.EffSlot, -1);
            }

            if (i < members.Length - 1 && ax.End is { Adjacent: true } end)
            {
                avail = Lin(avail, 1, end.EffSlot, -1);
            }
        }

        return avail;
    }

    /// <summary>
    /// Emits ops computing the span between the two anchors: (endTarget - effEnd) - (startTarget + effStart).
    /// </summary>
    private int EmitSpan(int axis, ResolvedAnchor start, ResolvedAnchor end)
    {
        var endPos = Lin(PoleSlot(axis, end), 1, end.EffSlot, -1);

        return Lin(endPos, 1, PoleSlot(axis, start), -1, start.EffSlot, -1);
    }

    private void EmitViewSize(int axis, int node)
    {
        var n = _nodes[node];
        var ax = n.Axes[axis];
        var other = n.Axes[1 - axis];
        var size = ax.Size;
        var view = (MagnetView) n.Node;
        var axisName = axis == 0 ? "Width" : "Height";

        // Where the measure happens: on the X axis, once per execution.
        var measureHere = axis == 0 && n.Measured;
        var widthMeasured = n.Axes[0].Size.Unit == MagnetSizingUnit.Measured;
        var heightMeasured = n.Axes[1].Size.Unit == MagnetSizingUnit.Measured;

        int? spanSlot = null;

        int Span()
        {
            if (spanSlot is { } s)
            {
                return s;
            }

            if (ax.Chain >= 0)
            {
                spanSlot = _nodes[ax.Chain].SpanSlot;
            }
            else if (ax.Start is { } start && ax.End is { } end)
            {
                spanSlot = EmitSpan(axis, start, end);

                // A fill-sized view still needs room for its margins when hugging.
                _pendingReqs.Add(spanSlot.Value);
            }
            else
            {
                throw new InvalidOperationException(
                    $"MagnetView '{n.Id}': {axisName} of unit {size.Unit} requires both {(axis == 0 ? "LeftTo and RightTo" : "TopTo and BottomTo")} (or a chain membership)."
                );
            }

            return spanSlot.Value;
        }

        int Avail()
        {
            if (ax.Chain >= 0)
            {
                return ax.Size.Unit == MagnetSizingUnit.Measured ? EmitChainMemberAvail(ax.Chain, node, axis) : _nodes[ax.Chain].SpanSlot;
            }

            if (ax.Start is { } s && ax.End is { } e)
            {
                return EmitSpan(axis, s, e);
            }

            if (ax.Start is { } so)
            {
                var pos = Lin(PoleSlot(axis, so), 1, so.EffSlot, 1);

                return Lin(StageArgSlot(axis), 1, pos, -1);
            }

            if (ax.End is { } eo)
            {
                return Lin(PoleSlot(axis, eo), 1, eo.EffSlot, -1);
            }

            return StageArgSlot(axis);
        }

        int ApplyBoundsAndVisibility(int raw)
        {
            var bounded = size.HasBounds ? Clamp(raw, ax.MinSlot, ax.MaxSlot) : raw;

            return MulAdd(bounded, n.VisSlot);
        }

        void EmitMeasure()
        {
            // width constraint
            int wc;

            if (widthMeasured || n.Axes[0].Size.Unit == MagnetSizingUnit.Ratio)
            {
                wc = Avail();

                if (widthMeasured && !double.IsPositiveInfinity(n.Axes[0].Size.Max))
                {
                    wc = Clamp(wc, MagnetTape.Zero, n.Axes[0].MaxSlot);
                }
            }
            else
            {
                wc = ax.SizeSlot; // already computed (fixed / constraint / percent)
            }

            // height constraint: fixed height if any, else the raw stage constraint
            int hc;

            if (n.Axes[1].Size.Unit == MagnetSizingUnit.Fixed)
            {
                hc = n.Axes[1].ValueSlot;
            }
            else
            {
                hc = MagnetTape.StageHeightArg;

                if (heightMeasured && !double.IsPositiveInfinity(n.Axes[1].Size.Max))
                {
                    hc = Clamp(hc, MagnetTape.Zero, n.Axes[1].MaxSlot);
                }
            }

            Emit(new Op(OpKind.MeasureChild, -1, node, wc, hc));
        }

        switch (size.Unit)
        {
            case MagnetSizingUnit.Fixed:
                ax.SizeSlot = ApplyBoundsAndVisibility(ax.ValueSlot);

                if (measureHere)
                {
                    EmitMeasure();
                }

                break;

            case MagnetSizingUnit.Measured:
            {
                if (measureHere)
                {
                    EmitMeasure();
                }

                var measured = axis == 0 ? n.MeasuredWidth : n.MeasuredHeight;

                // Optional scale (Value, patched as 1 when unset); collapsed views measure 0 anyway.
                var scaled = MulAdd(measured, ax.ValueSlot);
                ax.SizeSlot = size.HasBounds ? ApplyBoundsAndVisibility(scaled) : scaled;

                break;
            }

            case MagnetSizingUnit.StagePercent:
                ax.SizeSlot = ApplyBoundsAndVisibility(MulAdd(StageEndSlot(axis), ax.ValueSlot));

                if (measureHere)
                {
                    EmitMeasure();
                }

                break;

            case MagnetSizingUnit.Constraint:
                if (ax.Weighted)
                {
                    // Computed by the chain.
                    ax.SizeSlot = ax.WeightedSizeSlot;
                }
                else
                {
                    ax.SizeSlot = ApplyBoundsAndVisibility(Span());
                }

                if (measureHere)
                {
                    EmitMeasure();
                }

                break;

            case MagnetSizingUnit.ConstraintPercent:
                ax.SizeSlot = ApplyBoundsAndVisibility(MulAdd(Span(), ax.ValueSlot));

                if (measureHere)
                {
                    EmitMeasure();
                }

                break;

            case MagnetSizingUnit.Ratio:
            {
                int otherSize;

                switch (other.Size.Unit)
                {
                    case MagnetSizingUnit.Fixed:
                        otherSize = other.ValueSlot;

                        break;

                    case MagnetSizingUnit.Measured:
                        if (measureHere)
                        {
                            EmitMeasure();
                        }

                        otherSize = axis == 0 ? n.MeasuredHeight : other.SizeSlot;

                        break;

                    case MagnetSizingUnit.Ratio:
                        throw new InvalidOperationException($"MagnetView '{n.Id}': Width and Height cannot both be Ratio.");

                    default:
                        if (axis == 0)
                        {
                            // Ratio width fed by a Y-dependent height: read a feedback slot written by the Y pass
                            // (previous execution / previous pass); the executor re-runs X+Y once when it changes.
                            n.RatioFeedbackSlot = Alloc();
                            _feedbackSlots.Add(n.RatioFeedbackSlot);
                            otherSize = n.RatioFeedbackSlot;

                            break;
                        }

                        // Ratio height: the width (any unit) is final at this point.
                        otherSize = other.SizeSlot;

                        break;
                }

                if (otherSize < 0)
                {
                    throw new InvalidOperationException($"MagnetView '{n.Id}': internal error resolving Ratio {axisName}.");
                }

                var raw = MulAdd(otherSize, ax.ValueSlot);
                ax.SizeSlot = ApplyBoundsAndVisibility(raw);

                if (measureHere && other.Size.Unit != MagnetSizingUnit.Measured)
                {
                    EmitMeasure();
                }

                break;
            }

            default:
                throw new NotSupportedException($"{view.GetType().Name} '{n.Id}': unsupported size unit {size.Unit}.");
        }

        if (axis == 1 && n.RatioFeedbackSlot >= 0)
        {
            LinInto(n.RatioFeedbackSlot, ax.SizeSlot, 1);
        }

        if (axis == 1 && !n.Measured)
        {
            // MAUI contract: every child is measured each pass. Views with no Measured axis are measured
            // with their EXACT resolved sizes (like a Grid star cell) once both axes are known — skipping
            // this leaves platform containers with a zero DesiredSize and their content never laid out.
            Emit(new Op(OpKind.MeasureChild, -1, node, n.Axes[0].SizeSlot, ax.SizeSlot));
        }
    }

    private void EmitViewPosition(int axis, int node)
    {
        var n = _nodes[node];
        var ax = n.Axes[axis];

        if (ax.Chain >= 0)
        {
            return; // positioned by the chain
        }

        var startPole = axis == 0 ? 0 : 2;
        var endPole = axis == 0 ? 1 : 3;
        var size = ax.SizeSlot;

        if (ax.Start is { } start && ax.End is { } end)
        {
            var sp = Lin(PoleSlot(axis, start), 1, start.EffSlot, 1);
            var ep = Lin(PoleSlot(axis, end), 1, end.EffSlot, -1);
            var slack = Lin(ep, 1, sp, -1, size, -1);
            MulAddInto(n.Pole[startPole], slack, ax.BiasSlot, sp);
            LinInto(n.Pole[endPole], n.Pole[startPole], 1, size, 1);
            _pendingReqs.Add(slack);
        }
        else if (ax.Start is { } so)
        {
            LinInto(n.Pole[startPole], PoleSlot(axis, so), 1, so.EffSlot, 1);
            LinInto(n.Pole[endPole], n.Pole[startPole], 1, size, 1);
        }
        else if (ax.End is { } eo)
        {
            LinInto(n.Pole[endPole], PoleSlot(axis, eo), 1, eo.EffSlot, -1);
            LinInto(n.Pole[startPole], n.Pole[endPole], 1, size, -1);
        }
        else
        {
            LinInto(n.Pole[startPole], MagnetTape.Zero, 0);
            LinInto(n.Pole[endPole], size, 1);
        }
    }

    private void EmitChainSpan(int axis, int node)
    {
        var n = _nodes[node];
        var first = _nodes[n.Members[0]].Axes[axis];
        var last = _nodes[n.Members[^1]].Axes[axis];

        n.StartSlot = first.Start is { } s ? Lin(PoleSlot(axis, s), 1, s.EffSlot, 1) : StageStartSlot(axis);
        n.EndSlot = last.End is { } e ? Lin(PoleSlot(axis, e), 1, e.EffSlot, -1) : StageEndSlot(axis);
        n.SpanSlot = Lin(n.EndSlot, 1, n.StartSlot, -1);

        // Weighted members get their size slot up-front so downstream vertices can reference it.
        foreach (var m in n.Members)
        {
            var ax = _nodes[m].Axes[axis];

            if (ax.Weighted)
            {
                ax.WeightedSizeSlot = Alloc();
            }
        }
    }

    private void EmitChain(int axis, int node)
    {
        var n = _nodes[node];
        var chain = (MagnetChain) n.Node;
        var k = n.Members.Length;
        var startPole = axis == 0 ? 0 : 2;
        var endPole = axis == 0 ? 1 : 3;

        // Non-weighted sizes total
        var nwCount = 0;

        foreach (var m in n.Members)
        {
            if (!_nodes[m].Axes[axis].Weighted)
            {
                nwCount++;
            }
        }

        var totalNw = MagnetTape.Zero;

        if (nwCount > 0)
        {
            var block = _slots;
            _slots += nwCount;
            var b = block;

            foreach (var m in n.Members)
            {
                var ax = _nodes[m].Axes[axis];

                if (!ax.Weighted)
                {
                    LinInto(b++, ax.SizeSlot, 1);
                }
            }

            totalNw = Alloc();
            Emit(new Op(OpKind.SumRange, totalNw, block, nwCount, MagnetTape.Zero));
        }

        // Gaps (adjacent-anchor margins) total, per member "gap after"
        var gapAfter = new int[k]; // slot holding the gap after member i (Zero if none)
        var gapsTotal = MagnetTape.Zero;

        for (var i = 0; i < k; i++)
        {
            var g1 = MagnetTape.Zero;
            var g2 = MagnetTape.Zero;
            var ax = _nodes[n.Members[i]].Axes[axis];

            if (i < k - 1)
            {
                if (ax.End is { Adjacent: true } e)
                {
                    g1 = e.EffSlot;
                }

                var next = _nodes[n.Members[i + 1]].Axes[axis];

                if (next.Start is { Adjacent: true } s)
                {
                    g2 = s.EffSlot;
                }
            }

            if (g1 == MagnetTape.Zero && g2 == MagnetTape.Zero)
            {
                gapAfter[i] = MagnetTape.Zero;
            }
            else
            {
                gapAfter[i] = Lin(g1, 1, g2, 1);
                gapsTotal = Lin(gapsTotal, 1, gapAfter[i], 1);
            }
        }

        // Weighted sizes
        var totalW = MagnetTape.Zero;

        if (nwCount < k)
        {
            var dist = Lin(n.SpanSlot, 1, totalNw, -1, gapsTotal, -1);
            dist = Clamp(dist, MagnetTape.Zero, MagnetTape.PosInf);

            // Effective weights: a collapsed member contributes 0, so the visible members absorb its share
            // (fractions must be computed at runtime — visibility is not a patched input).
            var weightedCount = k - nwCount;
            var weightBlock = _slots;
            _slots += weightedCount;
            var gathered = weightBlock;

            for (var i = 0; i < k; i++)
            {
                var member = n.Members[i];

                if (_nodes[member].Axes[axis].Weighted)
                {
                    Emit(new Op(OpKind.Gather, gathered++, member, n.WeightSlots[i], MagnetTape.Zero, Coef(0)));
                }
            }

            var totalWeight = Alloc();
            Emit(new Op(OpKind.SumRange, totalWeight, weightBlock, weightedCount, MagnetTape.Zero));

            var gatheredSlot = weightBlock;

            for (var i = 0; i < k; i++)
            {
                var ax = _nodes[n.Members[i]].Axes[axis];

                if (!ax.Weighted)
                {
                    continue;
                }

                var fraction = Div(gatheredSlot++, totalWeight);
                var raw = MulAdd(dist, fraction);
                var bounded = ax.Size.HasBounds ? Clamp(raw, ax.MinSlot, ax.MaxSlot) : raw;
                MulAddInto(ax.WeightedSizeSlot, bounded, _nodes[n.Members[i]].VisSlot);
                ax.SizeSlot = ax.WeightedSizeSlot;
                totalW = Lin(totalW, 1, ax.WeightedSizeSlot, 1);
            }
        }

        var total = Lin(totalNw, 1, totalW, 1, gapsTotal, 1);
        var slack = Lin(n.SpanSlot, 1, total, -1);
        _pendingReqs.Add(slack);

        // Visible member count
        var visBlock = _slots;
        _slots += k;

        for (var i = 0; i < k; i++)
        {
            LinInto(visBlock + i, _nodes[n.Members[i]].VisSlot, 1);
        }

        var visCount = Alloc();
        Emit(new Op(OpKind.SumRange, visCount, visBlock, k, MagnetTape.Zero));

        var style = chain.Style;
        var gap = MagnetTape.Zero;
        var cursor = n.StartSlot;

        switch (style)
        {
            case MagnetChainStyle.Spread:
            {
                var den = Lin(visCount, 1, MagnetTape.One, 1);
                gap = Div(slack, den);

                break;
            }

            case MagnetChainStyle.SpreadInside:
            {
                var den = Lin(visCount, 1, MagnetTape.One, -1);
                den = Clamp(den, MagnetTape.One, MagnetTape.PosInf);
                gap = Div(slack, den);

                break;
            }

            case MagnetChainStyle.Packed:
            {
                var headBias = _nodes[n.Members[0]].Axes[axis].BiasSlot;
                cursor = MulAdd(slack, headBias, n.StartSlot);

                break;
            }
        }

        var prefixExcl = MagnetTape.Zero;

        for (var i = 0; i < k; i++)
        {
            var member = _nodes[n.Members[i]];
            var ax = member.Axes[axis];
            var visSlot = member.VisSlot;
            var prefixIncl = Lin(prefixExcl, 1, visSlot, 1);

            switch (style)
            {
                case MagnetChainStyle.Spread:
                    MulAddInto(member.Pole[startPole], gap, prefixIncl, cursor);

                    break;

                case MagnetChainStyle.SpreadInside:
                    MulAddInto(member.Pole[startPole], gap, prefixExcl, cursor);

                    break;

                default:
                    LinInto(member.Pole[startPole], cursor, 1);

                    break;
            }

            LinInto(member.Pole[endPole], member.Pole[startPole], 1, ax.SizeSlot, 1);
            cursor = Lin(cursor, 1, ax.SizeSlot, 1, gapAfter[i], 1);
            prefixExcl = prefixIncl;
        }
    }

    private void EmitBarrier(int axis, int node)
    {
        var n = _nodes[node];
        var barrier = (MagnetBarrier) n.Node;
        var k = n.Members.Length;
        var isMax = barrier.Direction is MagnetPole.Right or MagnetPole.Bottom;
        var slot = n.Pole[axis == 0 ? 0 : 2];

        if (k == 0)
        {
            LinInto(slot, MagnetTape.Zero, 0);

            return;
        }

        var block = _slots;
        _slots += k;
        var fallback = isMax ? double.NegativeInfinity : double.PositiveInfinity;

        for (var i = 0; i < k; i++)
        {
            var m = n.Members[i];
            var pole = _nodes[m].Pole[(int) barrier.Direction];
            Emit(new Op(OpKind.Gather, block + i, m, pole, MagnetTape.Zero, Coef(fallback)));
        }

        var range = Alloc();
        Emit(new Op(isMax ? OpKind.MaxRange : OpKind.MinRange, range, block, k, MagnetTape.Zero));
        LinInto(slot, range, 1, n.MarginSlot, isMax ? 1 : -1);
    }

    private void EmitGuideline(int axis, int node)
    {
        var n = _nodes[node];
        MulAddInto(n.Pole[axis == 0 ? 0 : 2], StageEndSlot(axis), n.PercentSlot, n.PositionSlot);
    }

    #endregion

    /// <summary>
    /// Debug helper: dumps the tape.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static string Dump(MagnetTape tape)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < tape.Ops.Length; i++)
        {
            sb.Append(i).Append(": ").AppendLine(tape.Ops[i].ToString(tape.Coefficients));
        }

        return sb.ToString();
    }
}
