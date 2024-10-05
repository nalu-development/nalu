namespace Nalu.Internals;

internal sealed class AsyncLazy<T>(Func<ValueTask<T>> factory, Action<T>? valueDisposer = null) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private T _value = default!;
    private bool _hasValue;

    public async ValueTask<T> GetValueAsync()
    {
        if (_hasValue)
        {
            return _value;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        if (_hasValue)
        {
            _semaphore.Release();
            return _value;
        }

        var value = await factory().ConfigureAwait(false);
        _value = value;
        _hasValue = true;
        _semaphore.Release();
        return value;
    }

    public void Dispose()
    {
        _semaphore.Wait();
        if (_hasValue)
        {
            if (valueDisposer is not null)
            {
                valueDisposer.Invoke(_value);
            }
            else
            {
                (_value as IDisposable)?.Dispose();
            }
        }

        _semaphore.Release();
        _semaphore.Dispose();
    }
}
