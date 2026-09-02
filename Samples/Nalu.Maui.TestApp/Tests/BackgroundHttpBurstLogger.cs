using Microsoft.Extensions.Logging;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// A logging provider that costs a configurable number of milliseconds PER CALL, scoped to the
/// background-session delegate's category only.
/// </summary>
/// <remarks>
/// <para>
/// This exists to make "how expensive is <c>ILogger</c> on the serial delegate queue" an
/// independent variable of the burst harness. The suspicion under test: a real app's provider
/// (Sentry, a flushing file sink, os_log with a debugger attached) does synchronous work inside
/// <see cref="ILogger.Log{TState}" />, and the background delegate calls it a dozen times per
/// completion on a SERIAL <c>NSOperationQueue</c>. Every millisecond spent there delays the
/// callbacks queued behind it, and their downloaded temp files are only guaranteed while their
/// own callback runs — so a slow logger should turn into "Failed to stage downloaded file"
/// (ENOENT) under a burst.
/// </para>
/// <para>
/// Scoped by category on purpose: a delay applied to EVERY logger would slow the whole app and
/// confound the measurement. <see cref="Thread.Sleep(int)" /> rather than an async delay because
/// the point is to occupy the delegate queue's thread exactly the way a synchronous sink does.
/// </para>
/// </remarks>
internal sealed class BackgroundHttpBurstLoggerProvider : ILoggerProvider
{
    /// <summary>Category substring of the delegate whose logging we are pricing.</summary>
    private const string TargetCategory = "MessageHandlerNSUrlSessionDownloadDelegate";

    private static int _delayMs;

    /// <summary>Milliseconds burned inside every Log call of the target category. 0 disables it.</summary>
    public static int DelayMs
    {
        get => Volatile.Read(ref _delayMs);
        set => Volatile.Write(ref _delayMs, value);
    }

    /// <summary>Log calls seen since the last <see cref="Reset" /> — the burst's on-queue call count.</summary>
    public static int Calls => Volatile.Read(ref _calls);

    private static int _calls;

    public static void Reset() => Volatile.Write(ref _calls, 0);

    public ILogger CreateLogger(string categoryName)
        => categoryName.Contains(TargetCategory, StringComparison.Ordinal)
            ? new DelayingLogger()
            : NullLogger.Instance;

    public void Dispose() { }

    private sealed class DelayingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        // Deliberately true for every level: this models a provider that ACCEPTS what it is
        // given, which is the case whose cost we are measuring. A provider that filters cheaply
        // is the null hypothesis and needs no simulation.
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Interlocked.Increment(ref _calls);

            var delay = DelayMs;

            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
