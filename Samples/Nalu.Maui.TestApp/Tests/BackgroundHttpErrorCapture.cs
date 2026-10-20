using Microsoft.Extensions.Logging;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Captures the background-session delegate's WARNING and ERROR logs so the lifecycle harness can
/// show — and UI tests can assert on — download failures such as a lost download whose file was
/// deleted before delivery ("… source file exists: False, downloads folder exists: …").
/// </summary>
/// <remarks>
/// PERSISTED (Preferences) until <see cref="Reset" />: iOS may deliver background-session events
/// in a background launch — a different process from the one the test reads afterwards.
/// </remarks>
internal sealed class BackgroundHttpErrorCapture : ILoggerProvider
{
    private const string TargetCategory = "MessageHandlerNSUrlSessionDownloadDelegate";
    private const string WarningsKey = "BackgroundHttpDelegateWarnings";
    private const string ErrorsKey = "BackgroundHttpDelegateErrors";
    private const string LastKey = "BackgroundHttpDelegateLast";

    // Per-signature counters: a later unrelated warning must not mask what a test asserts on.
    private static readonly (string Name, string Signature)[] _signatures =
    [
        ("folderMissing", "downloads folder exists: False"),
        ("secureFailed", "could not be secured"),
        ("handedToPrevious", "handed to a previous process")
    ];
    private static readonly Lock _gate = new();

    public static string Summary
    {
        get
        {
            lock (_gate)
            {
                var counts = FormattableString.Invariant(
                    $"delegateWarnings={Preferences.Default.Get(WarningsKey, 0)} delegateErrors={Preferences.Default.Get(ErrorsKey, 0)}"
                ) + string.Concat(_signatures.Select(sig => FormattableString.Invariant($" {sig.Name}={Preferences.Default.Get(SignatureKey(sig.Name), 0)}")));
                var last = Preferences.Default.Get(LastKey, string.Empty);

                return string.IsNullOrEmpty(last) ? counts : $"{counts} last={last}";
            }
        }
    }

    public static void Reset()
    {
        lock (_gate)
        {
            Preferences.Default.Remove(WarningsKey);
            Preferences.Default.Remove(ErrorsKey);
            Preferences.Default.Remove(LastKey);

            foreach (var (name, _) in _signatures)
            {
                Preferences.Default.Remove(SignatureKey(name));
            }
        }
    }

    private static string SignatureKey(string name) => $"BackgroundHttpDelegate.{name}";

    public ILogger CreateLogger(string categoryName)
        => categoryName.Contains(TargetCategory, StringComparison.Ordinal) ? new CapturingLogger() : NullLogger.Instance;

    public void Dispose() { }

    private sealed class CapturingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception) + (exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}");
            var key = logLevel >= LogLevel.Error ? ErrorsKey : WarningsKey;

            lock (_gate)
            {
                Preferences.Default.Set(key, Preferences.Default.Get(key, 0) + 1);
                Preferences.Default.Set(LastKey, $"{logLevel}: {message}");

                foreach (var (name, signature) in _signatures)
                {
                    if (message.Contains(signature, StringComparison.Ordinal))
                    {
                        Preferences.Default.Set(SignatureKey(name), Preferences.Default.Get(SignatureKey(name), 0) + 1);
                    }
                }
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
