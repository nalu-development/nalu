using System.Windows.Input;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The engine-routed selection command exposed as <see cref="ScaffoldRoot.SelectCommand"/>:
/// parameterless, resolves the owning <see cref="Scaffold"/> from the element tree at
/// execution time (no-op while detached), and shares the scaffold-wide selection gate —
/// while ANY selection navigates, every root's command reports non-executable, so bound
/// buttons disable together instead of racing an in-flight navigation the engine would
/// silently ignore.
/// </summary>
internal sealed class ScaffoldRootSelectCommand(ScaffoldRoot root) : ICommand
{
    private Scaffold? _observedScaffold;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => ObserveScaffold() is not { IsRootSelectionInFlight: true };

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (ObserveScaffold() is { } scaffold)
        {
            scaffold.SelectRootGatedAsync(root).FireAndForget(scaffold.Handler);
        }
    }

    /// <summary>
    /// Resolves the owning scaffold lazily (the command can be bound before the root is
    /// parented) and keeps the gate subscription pointed at it.
    /// </summary>
    private Scaffold? ObserveScaffold()
    {
        var scaffold = root.FindScaffold();

        if (!ReferenceEquals(scaffold, _observedScaffold))
        {
            if (_observedScaffold is not null)
            {
                _observedScaffold.RootSelectionInFlightChanged -= OnSelectionInFlightChanged;
            }

            _observedScaffold = scaffold;

            if (scaffold is not null)
            {
                scaffold.RootSelectionInFlightChanged += OnSelectionInFlightChanged;
            }
        }

        return scaffold;
    }

    private void OnSelectionInFlightChanged(object? sender, EventArgs e)
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
