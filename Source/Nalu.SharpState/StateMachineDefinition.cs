namespace Nalu.SharpState;

/// <summary>
/// Immutable, frozen configuration of a state machine type: the per-state <see cref="IStateConfiguration{TContext, TState, TTrigger, TActor}"/>
/// plus the hierarchy maps (<see cref="Parent"/>, <see cref="InitialChild"/>) and derived helpers.
/// Built once per machine type by the generated <c>BuildDefinition()</c> method and shared across every
/// <c>CreateActor(...)</c> call.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
/// <typeparam name="TActor">Type of the actor passed into post-transition reactions.</typeparam>
public sealed class StateMachineDefinition<TContext, TState, TTrigger, TActor>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly IReadOnlyDictionary<TState, IStateConfiguration<TContext, TState, TTrigger, TActor>> _states;
    private readonly Dictionary<TState, TState[]> _ancestorsCache;

    /// <summary>
    /// Initializes a new <see cref="StateMachineDefinition{TContext, TState, TTrigger, TActor}"/> by freezing
    /// the supplied per-state configurations and validating the hierarchy.
    /// </summary>
    /// <param name="states">Mapping from every declared state to its configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="states"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The hierarchy declared by the configurations is inconsistent
    /// (multi-parent, cycle, composite without children, child without a composite parent, etc.).</exception>
    public StateMachineDefinition(IReadOnlyDictionary<TState, IStateConfiguration<TContext, TState, TTrigger, TActor>> states)
    {
        _states = states ?? throw new ArgumentNullException(nameof(states));

        var parent = new Dictionary<TState, TState>();
        var initialChild = new Dictionary<TState, TState>();

        foreach (var kvp in _states)
        {
            var state = kvp.Key;
            var config = kvp.Value;
            if (config.ParentState is { } p)
            {
                parent[state] = p;
            }

            if (config.InitialChildState is { } c)
            {
                initialChild[state] = c;
            }
        }

        Parent = parent;
        InitialChild = initialChild;
        Validate();

        _ancestorsCache = new Dictionary<TState, TState[]>();
        foreach (var state in _states.Keys)
        {
            _ancestorsCache[state] = BuildAncestors(state);
        }
    }

    /// <summary>
    /// Mapping from each child state to its parent composite. States that are not children of any composite
    /// do not appear in the map.
    /// </summary>
    public IReadOnlyDictionary<TState, TState> Parent { get; }

    /// <summary>
    /// Mapping from each composite state to the child entered by default when the composite is targeted.
    /// Leaf states do not appear in the map.
    /// </summary>
    public IReadOnlyDictionary<TState, TState> InitialChild { get; }

    /// <summary>
    /// The full set of states known to this definition.
    /// </summary>
    public IReadOnlyCollection<TState> States => (IReadOnlyCollection<TState>) _states.Keys;

    /// <summary>
    /// Retrieves the <see cref="IStateConfiguration{TContext, TState, TTrigger, TActor}"/> for a given state.
    /// </summary>
    /// <param name="state">The state to look up.</param>
    /// <returns>The configuration associated with <paramref name="state"/>.</returns>
    /// <exception cref="KeyNotFoundException">No configuration exists for <paramref name="state"/>.</exception>
    public IStateConfiguration<TContext, TState, TTrigger, TActor> GetConfiguration(TState state)
        => _states.TryGetValue(state, out var c)
            ? c
            : throw new KeyNotFoundException($"State '{state}' is not registered in the state machine definition.");

    /// <summary>
    /// Attempts to retrieve the <see cref="IStateConfiguration{TContext, TState, TTrigger, TActor}"/> for a given state.
    /// </summary>
    /// <param name="state">The state to look up.</param>
    /// <param name="configuration">When the method returns <c>true</c>, the associated configuration.</param>
    /// <returns><c>true</c> if a configuration is registered for <paramref name="state"/>.</returns>
    public bool TryGetConfiguration(TState state, out IStateConfiguration<TContext, TState, TTrigger, TActor> configuration)
        => _states.TryGetValue(state, out configuration!);

    /// <summary>
    /// Follows <see cref="InitialChild"/> from <paramref name="state"/> until a leaf state is reached.
    /// Returns <paramref name="state"/> unchanged when it is already a leaf.
    /// </summary>
    /// <param name="state">The state to resolve.</param>
    /// <returns>The deepest initial-child leaf of <paramref name="state"/>.</returns>
    public TState LeafOf(TState state)
    {
        var current = state;
        while (InitialChild.TryGetValue(current, out var child))
        {
            current = child;
        }

        return current;
    }

    /// <summary>
    /// Returns the chain of ancestors of <paramref name="state"/> (excluding <paramref name="state"/> itself),
    /// from immediate parent up to the topmost composite. Empty when <paramref name="state"/> has no parent.
    /// </summary>
    /// <param name="state">The state whose ancestors should be enumerated.</param>
    /// <returns>An ordered list of ancestor states.</returns>
    public IReadOnlyList<TState> AncestorsOf(TState state)
        => _ancestorsCache.TryGetValue(state, out var cached) ? cached : Array.Empty<TState>();

    /// <summary>
    /// Determines whether <paramref name="state"/> equals <paramref name="ancestor"/> or is a transitive child of it.
    /// </summary>
    /// <param name="state">The candidate descendant.</param>
    /// <param name="ancestor">The candidate ancestor.</param>
    /// <returns><c>true</c> if <paramref name="state"/> is <paramref name="ancestor"/> or one of its descendants.</returns>
    public bool IsSelfOrDescendantOf(TState state, TState ancestor)
    {
        if (EqualityComparer<TState>.Default.Equals(state, ancestor))
        {
            return true;
        }

        foreach (var a in AncestorsOf(state))
        {
            if (EqualityComparer<TState>.Default.Equals(a, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Computes the lowest common ancestor of <paramref name="a"/> and <paramref name="b"/>, if any.
    /// </summary>
    /// <param name="a">The first state.</param>
    /// <param name="b">The second state.</param>
    /// <returns>The nearest common ancestor of both states, or <c>null</c> when they belong to disjoint subtrees.</returns>
    public TState? LowestCommonAncestor(TState a, TState b)
    {
        var comparer = EqualityComparer<TState>.Default;
        var aChain = new List<TState> { a };
        aChain.AddRange(AncestorsOf(a));

        var current = b;
        while (true)
        {
            foreach (var x in aChain)
            {
                if (comparer.Equals(x, current))
                {
                    return current;
                }
            }

            if (!Parent.TryGetValue(current, out var next))
            {
                return null;
            }

            current = next;
        }
    }

    private TState[] BuildAncestors(TState state)
    {
        var list = new List<TState>();
        var current = state;
        while (Parent.TryGetValue(current, out var p))
        {
            list.Add(p);
            current = p;
        }

        return list.ToArray();
    }

    private void Validate()
    {
        var comparer = EqualityComparer<TState>.Default;

        foreach (var kvp in Parent)
        {
            var child = kvp.Key;
            var parent = kvp.Value;
            if (!_states.ContainsKey(parent))
            {
                throw new InvalidOperationException(
                    $"State '{child}' declares parent '{parent}' but '{parent}' is not registered as a state.");
            }

            if (!InitialChild.ContainsKey(parent))
            {
                throw new InvalidOperationException(
                    $"State '{child}' declares parent '{parent}' but '{parent}' does not declare an initial child via a nested [SubStateMachine(parent: {parent})] region with one [StateDefinition(Initial = true)] child.");
            }
        }

        foreach (var kvp in InitialChild)
        {
            var composite = kvp.Key;
            var initial = kvp.Value;
            if (!_states.ContainsKey(initial))
            {
                throw new InvalidOperationException(
                    $"State '{composite}' declares initial child '{initial}' but '{initial}' is not registered as a state.");
            }

            if (!Parent.TryGetValue(initial, out var initialsParent) || !comparer.Equals(initialsParent, composite))
            {
                throw new InvalidOperationException(
                    $"State '{composite}' declares initial child '{initial}', but '{initial}' is not nested inside a [SubStateMachine(parent: {composite})] region.");
            }
        }

        foreach (var state in _states.Keys)
        {
            var seen = new HashSet<TState>(comparer) { state };
            var current = state;
            while (Parent.TryGetValue(current, out var p))
            {
                if (!seen.Add(p))
                {
                    throw new InvalidOperationException(
                        $"Cycle detected in state hierarchy involving state '{state}'.");
                }

                current = p;
            }
        }
    }
}
