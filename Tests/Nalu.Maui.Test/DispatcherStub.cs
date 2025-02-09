using System.Diagnostics;

namespace Nalu.Maui.Test;

// ReSharper disable all
// Code imported from MAUI repo test projects
#pragma warning disable IDE0290, IDE0040, SA1400, SA1402, CA1001, SA1308, IDE0044, IDE1006, CA2211, SA1401, VSTHRD101, IDE0058, IDE0061, IDE0022

public class DispatcherStub : IDispatcher
{
    readonly Func<bool>? _isInvokeRequired;
    readonly Action<Action>? _invokeOnMainThread;

    public DispatcherStub(Func<bool>? isInvokeRequired, Action<Action>? invokeOnMainThread)
    {
        _isInvokeRequired = isInvokeRequired;
        _invokeOnMainThread = invokeOnMainThread;

        ManagedThreadId = Environment.CurrentManagedThreadId;
    }

    public bool IsDispatchRequired =>
        _isInvokeRequired?.Invoke() ?? false;

    public int ManagedThreadId { get; }

    public bool Dispatch(Action action)
    {
        if (_invokeOnMainThread is null)
        {
            action();
        }
        else
        {
            _invokeOnMainThread.Invoke(action);
        }

        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        Timer? timer = null;

        timer = new Timer(OnTimeout, null, delay, delay);

        return true;

        void OnTimeout(object? state)
        {
            Dispatch(action);

            timer?.Dispose();
        }
    }

    public IDispatcherTimer CreateTimer()
    {
        return new DispatcherTimerStub(this);
    }
}

public class DispatcherTimerStub : IDispatcherTimer
{
    readonly DispatcherStub _dispatcher;

    Timer? _timer;

    public DispatcherTimerStub(DispatcherStub dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public TimeSpan Interval { get; set; }

    public bool IsRepeating { get; set; }

    public bool IsRunning => _timer != null;

    public event EventHandler? Tick;

    public void Start()
    {
        _timer = new Timer(OnTimeout, null, Interval, IsRepeating ? Interval : Timeout.InfiniteTimeSpan);

        void OnTimeout(object? state)
        {
            _dispatcher.Dispatch(() => Tick?.Invoke(this, EventArgs.Empty));
        }
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}

public sealed class DispatcherProviderStub : IDispatcherProvider, IDisposable
{
    ThreadLocal<IDispatcher?> s_dispatcherInstance = new(
        () =>
            DispatcherProviderStubOptions.SkipDispatcherCreation
                ? null
                : new DispatcherStub(
                    DispatcherProviderStubOptions.IsInvokeRequired,
                    DispatcherProviderStubOptions.InvokeOnMainThread
                )
    );

    public void Dispose() =>
        s_dispatcherInstance.Dispose();

    public IDispatcher? GetForCurrentThread()
    {
        var x = s_dispatcherInstance.Value;

        if (x == null)
        {
            Debug.WriteLine("WTH");
        }

        return x;
    }
}

public class DispatcherProviderStubOptions
{
    [ThreadStatic]
    public static bool SkipDispatcherCreation;

    [ThreadStatic]
    public static Func<bool>? IsInvokeRequired;

    [ThreadStatic]
    public static Action<Action>? InvokeOnMainThread;
}

public static class DispatcherTest
{
    public static Task Run(Action testAction) =>
        Run(
            () =>
            {
                testAction();

                return Task.CompletedTask;
            }
        );

    public static Task RunWithDispatcherStub(Action testAction) =>
        Run(
            () =>
            {
                DispatcherProvider.SetCurrent(new DispatcherProviderStub());
                testAction();

                return Task.CompletedTask;
            }
        );

    public static Task Run(Func<Task> testAction)
    {
        var tcs = new TaskCompletionSource();

        var thread = new Thread(
            async () =>
            {
                try
                {
                    await testAction();

                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }
        );

        thread.Start();

        return tcs.Task;
    }

    public static Task RunWithDispatcherStub(Func<Task> testAction) =>
        Run(
            () =>
            {
                DispatcherProvider.SetCurrent(new DispatcherProviderStub());

                return testAction();
            }
        );
}
