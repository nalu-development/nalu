namespace Nalu.SharpState;

/// <summary>
/// Generic base class for per-machine state configurators. Holds the accumulated per-trigger transition list,
/// the optional hierarchy declarations (<c>Parent</c>, <c>AsStateMachine</c>), and implements
/// <see cref="IStateConfiguration{TContext, TState, TTrigger}"/> so the chain can be returned directly from a
/// <c>[StateDefinition]</c> property without a separate <c>Build()</c> step.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
public abstract class StateConfigurator<TContext, TState, TTrigger>
    : IStateConfiguration<TContext, TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly Dictionary<TTrigger, List<Transition<TContext, TState>>> _transitions = new();
    private TState? _parent;
    private TState? _initialChild;

    /// <inheritdoc />
    public TState? ParentState => _parent;

    /// <inheritdoc />
    public TState? InitialChildState => _initialChild;

    /// <inheritdoc />
    public bool TryGetTransitions(TTrigger trigger, out IReadOnlyList<Transition<TContext, TState>> transitions)
    {
        if (_transitions.TryGetValue(trigger, out var list))
        {
            transitions = list;
            return true;
        }

        transitions = Array.Empty<Transition<TContext, TState>>();
        return false;
    }

    /// <summary>
    /// Appends the supplied transitions to the per-trigger bucket. Generated <c>On&lt;Trigger&gt;</c> methods call this
    /// after running the user's configuration lambda and validating the resulting <see cref="StateTriggerBuilderBase{TContext, TState}"/>.
    /// </summary>
    /// <param name="trigger">The trigger the transitions belong to.</param>
    /// <param name="transitions">The transitions to add.</param>
    protected void AddTransitions(TTrigger trigger, IReadOnlyList<Transition<TContext, TState>> transitions)
    {
        if (transitions.Count == 0)
        {
            return;
        }

        if (!_transitions.TryGetValue(trigger, out var list))
        {
            list = new List<Transition<TContext, TState>>(transitions.Count);
            _transitions[trigger] = list;
        }

        list.AddRange(transitions);
    }

    /// <summary>
    /// Declares that this state is a child of <paramref name="parent"/>.
    /// May be called at most once per configurator.
    /// </summary>
    /// <param name="parent">The composite parent state.</param>
    /// <exception cref="InvalidOperationException"><c>Parent</c> has already been set on this configurator.</exception>
    protected void SetParent(TState parent)
    {
        if (_parent is not null)
        {
            throw new InvalidOperationException("Parent has already been set on this state configurator.");
        }

        _parent = parent;
    }

    /// <summary>
    /// Declares that this state behaves as a sub-state-machine whose initial leaf is <paramref name="initial"/>.
    /// May be called at most once per configurator.
    /// </summary>
    /// <param name="initial">The child state entered when this composite is targeted.</param>
    /// <exception cref="InvalidOperationException"><c>AsStateMachine</c> has already been set on this configurator.</exception>
    protected void SetInitialChild(TState initial)
    {
        if (_initialChild is not null)
        {
            throw new InvalidOperationException("AsStateMachine has already been set on this state configurator.");
        }

        _initialChild = initial;
    }
}
