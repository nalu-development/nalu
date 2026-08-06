using System.Windows.Input;

namespace Nalu.Internals;

/// <summary>
/// A minimal non-reentrant async <see cref="ICommand"/>: <see cref="CanExecute"/> is false while
/// the running task is in flight, so rapid repeat invocations (fast taps on a back button) are
/// swallowed instead of queuing duplicate operations.
/// </summary>
/// <remarks>
/// Deliberately simpler than CommunityToolkit's AsyncRelayCommand: no parameters, no
/// cancellation, no observable execution state. Exceptions follow the
/// <see cref="NaluTaskExtensions.FireAndForget{T}(Task, T, string?)"/> policy — logged through
/// the element handler resolved at invocation time (and rethrown in DEBUG).
/// </remarks>
internal sealed class NaluAsyncCommand(Func<Task> execute, Func<IElementHandler?>? handlerProvider = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning;

    public void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        RunAsync().FireAndForget(handlerProvider?.Invoke());
    }

    private async Task RunAsync()
    {
        try
        {
            await execute().ConfigureAwait(true);
        }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
