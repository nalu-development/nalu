using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace Nalu.Maui.Sample.PageModels;

public class DispatcherScheduler(IDispatcher dispatcher) : IScheduler
{
    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
    {
        if (dispatcher.IsDispatchRequired)
        {
            dispatcher.Dispatch(() => action(this, state));
        }
        else
        {
            action(this, state);
        }

        return Disposable.Empty;
    }

    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        if (dueTime > TimeSpan.Zero)
        {
            dispatcher.DispatchDelayed(dueTime, () => action(this, state));

            return Disposable.Empty;
        }

        return Schedule(state, action);
    }

    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var dueIn = dueTime - Now;

        return Schedule(state, dueIn, action);
    }

    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
