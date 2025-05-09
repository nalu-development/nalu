using Nalu.Internals;

namespace Nalu.Cassowary;

/// <summary>
/// The constraint solver class.
/// </summary>
public partial class Solver
{
    private readonly RefDictionary<Constraint, Tag> _cnMap = new();
    private readonly RefDictionary<Symbol, Row> _rowMap = new(SymbolDictionaryComparer.Instance);
    private readonly RefDictionary<Variable, Symbol> _varMap = new();
    private readonly RefDictionary<Variable, EditInfo> _editMap = new();
    private readonly List<Symbol> _infeasibleRows = new();
    private readonly Row _objective = new();
    private Row? _artificial;
    private int _symbolIdTick;

    /// <summary>
    /// The max number of solver iterations before an error
    /// is thrown, in order to prevent infinite iteration. Default: `10,000`.
    /// </summary>
    public int MaxIterations { get; set; } = 1000;

    /// <summary>
    /// Construct a new Solver.
    /// </summary>
    public Solver()
    {
        // Constructor logic will be added here if needed,
        // but the default constructor is sufficient for now.
    }

    /// <summary>
    /// Test whether a value is approximately zero.
    /// </summary>
    private static bool NearZero(double value)
    {
        const double eps = 1.0e-8;
        return value < 0.0 ? -value < eps : value < eps;
    }
}
