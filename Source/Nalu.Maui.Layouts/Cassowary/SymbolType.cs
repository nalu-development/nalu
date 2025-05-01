namespace Nalu.Cassowary;

/// <summary>
/// Represents different types of symbols used in the Cassowary algorithm.
/// </summary>
internal enum SymbolType
{
    /// <summary>
    /// Represents an invalid or uninitialized symbol.
    /// </summary>
    Invalid,

    /// <summary>
    /// Represents external variables. These are the variables that you want to solve for.
    /// </summary>
    External,

    /// <summary>
    /// Represents slack variables. These are the variables that are used to satisfy the constraints in the system.
    /// </summary>
    Slack,

    /// <summary>
    /// Represents error variables. These are the variables that are used to handle constraints that cannot be satisfied.
    /// </summary>
    Error,

    /// <summary>
    /// Represents dummy variables. These are the variables that are used for internal purposes.
    /// </summary>
    Dummy
}
