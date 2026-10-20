using System.Net;

namespace Nalu;

// ReSharper disable once InconsistentNaming
internal class NSUrlRequestHandle(
    string identifier,
    CookieContainer? cookieContainer,
    string? contentFile,
    bool isLostRequest = false
)
{
    private readonly TaskCompletionSource _completedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _completionLock = new();
    private bool _completed;
    public TaskCompletionSource<HttpResponseMessage> ResponseCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task CompletedTask => _completedCompletionSource.Task;
    public string Identifier => identifier;
    public bool IsLostRequest => isLostRequest;
    public CookieContainer? CookieContainer => cookieContainer;
    public string? ResponseContentFile { get; set; }

    /// <summary>
    /// The native task this handle belongs to. Request identifiers are caller-chosen and survive
    /// relaunches, so a callback is only ever applied to the handle created for its very task.
    /// </summary>
    public nuint TaskIdentifier { get; init; }

    /// <summary>Whether a second send with the same identifier may attach to this transfer.</summary>
    public bool IsAttachable => !isLostRequest && !ResponseCompletionSource.Task.IsCompleted;

    public CancellationTokenRegistration? CancellationTokenRegistration;

    public void Complete()
    {
        lock (_completionLock)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
        }

        CancellationTokenRegistration?.Dispose();
        _completedCompletionSource.TrySetResult();

        if (File.Exists(contentFile))
        {
            File.Delete(contentFile);
        }

        if (File.Exists(ResponseContentFile))
        {
            File.Delete(ResponseContentFile);
        }
    }
}
