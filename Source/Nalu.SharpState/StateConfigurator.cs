namespace Nalu.SharpState;

/// <summary>
/// Generic base class for per-machine state configurators. Holds the accumulated per-trigger transition list,
/// the optional hierarchy declarations (<c>Parent</c>, <c>AsStateMachine</c>), and implements
/// <see cref="IStateConfiguration{TContext, TState, TTrigger, TActor}"/> so the chain can be returned directly from a
/// <c>[StateDefinition]</c> property without a separate <c>Build()</c> step.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
/// <typeparam name="TActor">Type of the actor passed into post-transition reactions.</typeparam>
public abstract class StateConfigurator<TContext, TState, TTrigger, TActor>
    : IStateConfiguration<TContext, TState, TTrigger, TActor>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly Dictionary<TTrigger, List<Transition<TContext, TState, TActor>>> _transitions = new();
    private TState? _parent;
    private TState? _initialChild;
    private Action<TContext>? _entryAction;
    private Action<TContext>? _exitAction;

    /// <inheritdoc />
    public TState? ParentState => _parent;

    /// <inheritdoc />
    public TState? InitialChildState => _initialChild;

    /// <inheritdoc />
    public Action<TContext>? EntryAction => _entryAction;

    /// <inheritdoc />
    public Action<TContext>? ExitAction => _exitAction;

    /// <inheritdoc />
    public bool TryGetTransitions(TTrigger trigger, out IReadOnlyList<Transition<TContext, TState, TActor>> transitions)
    {
        if (_transitions.TryGetValue(trigger, out var list))
        {
            transitions = list;
            return true;
        }

        transitions = Array.Empty<Transition<TContext, TState, TActor>>();
        return false;
    }

    /// <summary>
    /// Appends the supplied transitions to the per-trigger bucket. Generated <c>On&lt;Trigger&gt;</c> methods call this
    /// after running the user's configuration lambda and validating the resulting <see cref="StateTriggerBuilderBase{TContext, TState, TActor}"/>.
    /// </summary>
    /// <param name="trigger">The trigger the transitions belong to.</param>
    /// <param name="transitions">The transitions to add.</param>
    protected void AddTransitions(TTrigger trigger, IReadOnlyList<Transition<TContext, TState, TActor>> transitions)
    {
        if (transitions.Count == 0)
        {
            return;
        }

        if (!_transitions.TryGetValue(trigger, out var list))
        {
            list = new List<Transition<TContext, TState, TActor>>(transitions.Count);
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

    /// <summary>
    /// Declares a synchronous callback to run after the machine enters this state.
    /// May be called at most once per configurator.
    /// </summary>
    protected void SetEntryAction(Action<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_entryAction is not null)
        {
            throw new InvalidOperationException("WhenEntering has already been set on this state configurator.");
        }

        _entryAction = action;
    }

    /// <summary>
    /// Declares a synchronous callback to run before the machine exits this state.
    /// May be called at most once per configurator.
    /// </summary>
    protected void SetExitAction(Action<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_exitAction is not null)
        {
            throw new InvalidOperationException("WhenExiting has already been set on this state configurator.");
        }

        _exitAction = action;
    }
}
