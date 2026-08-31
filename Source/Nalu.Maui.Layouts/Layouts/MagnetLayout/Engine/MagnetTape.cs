using System.Runtime.InteropServices;

namespace Nalu.MagnetLayout.Engine;

/// <summary>
/// The instruction set of the Magnet tape.
/// </summary>
internal enum OpKind : byte
{
    /// <summary>dst = K1·v[A] + K2·v[B] + K3·v[C]</summary>
    LinComb,

    /// <summary>dst = v[A]·v[B] + v[C] (v[B] is always a scalar input: bias, percent, ratio, weight, visibility…)</summary>
    MulAdd,

    /// <summary>dst = v[A] / v[B] (v[B] is always a scalar with no dependency on the stage size)</summary>
    Div,

    /// <summary>dst = min(v[A..A+B-1]); ±∞ results collapse to 0</summary>
    MinRange,

    /// <summary>dst = max(v[A..A+B-1]); ±∞ results collapse to 0</summary>
    MaxRange,

    /// <summary>dst = Σ v[A..A+B-1]</summary>
    SumRange,

    /// <summary>dst = clamp(v[A], v[B], v[C])</summary>
    Clamp,

    /// <summary>dst = node A visible ? v[B] : K1</summary>
    Gather,

    /// <summary>Measures the view bound to node A with (v[B], v[C]) constraints; writes the measured slots of the node.</summary>
    MeasureChild,

    /// <summary>Resolves the stage end slot (Dst) of axis C from the requirement slots ReqSlots[A..A+B-1].</summary>
    StageEnd
}

/// <summary>
/// One tape instruction (20 bytes). Coefficients are structural constants stored in <see cref="MagnetTape.Coefficients" />
/// and referenced by index; every runtime number lives in a slot.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal readonly struct Op(OpKind kind, int dst, int a, int b, int c, byte k1 = 0, byte k2 = 0, byte k3 = 0)
{
    public readonly int Dst = dst;
    public readonly int A = a;
    public readonly int B = b;
    public readonly int C = c;
    public readonly OpKind Kind = kind;
    public readonly byte K1 = k1;
    public readonly byte K2 = k2;
    public readonly byte K3 = k3;

    public string ToString(double[] coefficients)
        => Kind switch
        {
            OpKind.LinComb => $"v[{Dst}] = {coefficients[K1]}*v[{A}] + {coefficients[K2]}*v[{B}] + {coefficients[K3]}*v[{C}]",
            OpKind.MulAdd => $"v[{Dst}] = v[{A}]*v[{B}] + v[{C}]",
            OpKind.Div => $"v[{Dst}] = v[{A}] / v[{B}]",
            OpKind.MinRange => $"v[{Dst}] = min(v[{A}..{A + B - 1}])",
            OpKind.MaxRange => $"v[{Dst}] = max(v[{A}..{A + B - 1}])",
            OpKind.SumRange => $"v[{Dst}] = sum(v[{A}..{A + B - 1}])",
            OpKind.Clamp => $"v[{Dst}] = clamp(v[{A}], v[{B}], v[{C}])",
            OpKind.Gather => $"v[{Dst}] = vis[{A}] ? v[{B}] : {coefficients[K1]}",
            OpKind.MeasureChild => $"measure node {A} with (v[{B}], v[{C}])",
            OpKind.StageEnd => $"v[{Dst}] = stageEnd(axis {C}, reqs {A}..{A + B - 1})",
            _ => Kind.ToString()
        };
}

/// <summary>
/// Kinds of patchable input values.
/// </summary>
internal enum PatchKind : byte
{
    AnchorMargin,      // Aux = (int) MagnetPole side of the view
    AnchorGoneMargin,  // Aux = (int) MagnetPole side of the view
    SizeValue,         // Aux = axis
    SizeMin,           // Aux = axis
    SizeMax,           // Aux = axis
    BiasX,
    BiasY,
    BarrierMargin,
    GuidelinePercent,
    GuidelinePosition,
    ChainWeightFraction // Aux = member position
}

/// <summary>
/// (node, property) → input slot.
/// </summary>
internal readonly record struct PatchEntry(int Node, PatchKind Kind, int Aux, int Slot);

/// <summary>
/// Effective margin selection: eff = selfCollapsed ? 0 : (targetCollapsed ? gone : margin).
/// </summary>
internal readonly record struct MarginEntry(int SelfNode, int TargetNode, int MarginSlot, int GoneSlot, int EffSlot);

/// <summary>
/// Per-node compiled metadata.
/// </summary>
internal struct NodeMeta
{
    public bool IsView;
    public int VisSlot;
    public int Left, Top, Right, Bottom; // for virtual nodes the axis pole slots coincide (Left == Right or Top == Bottom)
    public int MeasuredWidth, MeasuredHeight;
}

/// <summary>
/// Op index boundaries of one axis.
/// </summary>
internal struct AxisPhases
{
    /// <summary>First op of the axis (phase 0: independent of the stage end).</summary>
    public int Start;

    /// <summary>First op depending on the stage end (phase 1).</summary>
    public int OneStart;

    /// <summary>First requirement op.</summary>
    public int ReqStart;

    /// <summary>Index of the StageEnd op.</summary>
    public int StageEndOp;

    /// <summary>One past the last op of the axis.</summary>
    public int End;

    /// <summary>Phase 1 contains piecewise ops (clamp/min/max): a second hug refinement pass is worth running.</summary>
    public bool HasPiecewise;
}

/// <summary>
/// The compiled layout: pure data, no reference to the layout instance nor to the nodes.
/// </summary>
internal sealed class MagnetTape
{
    public required Op[] Ops { get; init; }

    /// <summary>Structural coefficients referenced by <see cref="Op.K1" />/<see cref="Op.K2" />/<see cref="Op.K3" />. Index 0 = 0, 1 = 1, 2 = -1.</summary>
    public required double[] Coefficients { get; init; }
    public required int ValueCount { get; init; }
    /// <summary>Requirement entries: slot ≥ 0 means "v[slot] ≥ 0", ~slot means "v[slot] ≤ stageEnd".</summary>
    public required int[] ReqSlots { get; init; }
    public required AxisPhases X { get; init; }
    public required AxisPhases Y { get; init; }
    public required NodeMeta[] Nodes { get; init; }
    public required PatchEntry[] Patches { get; init; }
    public required MarginEntry[] Margins { get; init; }
    public required int InputStart { get; init; }
    public required int InputEnd { get; init; }

    /// <summary>
    /// Slots feeding a Ratio width from a Y-dependent height (cross-axis feedback). Empty for most layouts:
    /// when non-empty the executor runs a second X+Y pass if any of them changed during the Y pass.
    /// </summary>
    public required int[] FeedbackSlots { get; init; }

    public bool HasFeedback => FeedbackSlots.Length > 0;

    public const int StageLeft = 0;
    public const int StageTop = 1;
    public const int StageRight = 2;
    public const int StageBottom = 3;
    public const int StageWidthArg = 4;
    public const int StageHeightArg = 5;
    public const int Zero = 6;
    public const int One = 7;
    public const int PosInf = 8;
    public const int NegInf = 9;
    public const int FixedSlots = 10;
}
