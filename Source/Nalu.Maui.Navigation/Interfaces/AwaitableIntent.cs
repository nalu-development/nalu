using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// Base class for intents that can be awaited.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public abstract class AwaitableIntent<T> : IAwaitableIntentController
{
    private readonly TaskCompletionSource<T> _tcs = new();
    private T _result = default!;
    private Exception? _exception;

    /// <summary>
    /// Sets the result of the intent.
    /// </summary>
    /// <param name="result"></param>
    public void SetResult(T result) => _result = result;

    /// <summary>
    /// Transitions the intent into a failed state with the given exception.
    /// This will cause the awaiter to throw the exception when awaited.
    /// </summary>
    /// <param name="ex"></param>
    public void SetException(Exception ex) => _exception = ex;

    void IAwaitableIntentController.Complete()
    {
        if (_exception != null)
        {
            _tcs.TrySetException(_exception);
        }
        else
        {
            _tcs.TrySetResult(_result);
        }
    }

    /// <summary>
    /// Gets an awaiter to await this <see cref="AwaitableIntent{T}"/>.
    /// </summary>
    public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();
}

/// <summary>
/// Base class for intents that can be awaited.
/// </summary>
public abstract class AwaitableIntent : IAwaitableIntentController
{
    private readonly TaskCompletionSource _tcs = new();
    private Exception? _exception;

    /// <summary>
    /// Transitions the intent into a failed state with the given exception.
    /// This will cause the awaiter to throw the exception when awaited.
    /// </summary>
    /// <param name="ex"></param>
    public void SetException(Exception ex) => _exception = ex;

    void IAwaitableIntentController.Complete()
    {
        if (_exception != null)
        {
            _tcs.TrySetException(_exception);
        }
        else
        {
            _tcs.TrySetResult();
        }
    }

    /// <summary>
    /// Gets an awaiter to await this <see cref="AwaitableIntent{T}"/>.
    /// </summary>
    public TaskAwaiter GetAwaiter() => _tcs.Task.GetAwaiter();
}
