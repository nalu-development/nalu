namespace Nalu;

#pragma warning disable IDE0032

/// <summary>
/// Represents a handle to a <see cref="BackgroundHttpRequestMessage"/>.
/// This class is used to track the status of a background HTTP request.
/// </summary>
public class BackgroundHttpRequestHandle : Internals.BaseObservableObject
{
    private readonly BackgroundHttpRequestDescriptor _descriptor;
    private readonly TaskCompletionSource _acknowledgeCompletionSource = new();
    private readonly TaskCompletionSource<HttpResponseMessage> _resultCompletionSource = new();
    private readonly IBackgroundHttpRequestManager _manager;

    /// <summary>
    /// Gets the named unique identifier of the request.
    /// </summary>
    public string RequestName => _descriptor.RequestName;

    /// <summary>
    /// Gets the description of the request.
    /// </summary>
    public string? UserDescription => _descriptor.UserDescription;

    /// <summary>
    /// Gets a value indicating whether the request is a multi-part one.
    /// </summary>
    public bool IsMultiPart => _descriptor.IsMultiPart;

    /// <summary>
    /// Gets the progress of the request.
    /// </summary>
    public float Progress => _descriptor.Progress;

    /// <summary>
    /// Gets a value indicating whether the request has completed.
    /// </summary>
    public BackgroundHttpRequestState State => _descriptor.State;

    /// <summary>
    /// Gets the unique request identifier.
    /// </summary>
    public long RequestId { get; init; }

    internal BackgroundHttpRequestDescriptor Descriptor => _descriptor;

    internal Task AcknowledgeTask => _acknowledgeCompletionSource.Task;

    internal BackgroundHttpRequestHandle(IBackgroundHttpRequestManager manager, BackgroundHttpRequestDescriptor descriptor)
    {
        _descriptor = descriptor;
        _manager = manager;
    }

    /// <summary>
    /// Gets the awaitable task to retrieve the <see cref="HttpResponseMessage"/> once available.
    /// </summary>
    public Task<HttpResponseMessage> GetResultAsync()
    {
        if (State == BackgroundHttpRequestState.Completed && !_resultCompletionSource.Task.IsCompleted)
        {
            throw new NotSupportedException("Future retrieval of completed requests is not supported.");
        }

        return _resultCompletionSource.Task;
    }

    /// <summary>
    /// Acknowledges the response has been received and can be removed from the background requests.
    /// </summary>
    /// <exception cref="InvalidOperationException">when <see cref="BackgroundHttpClient"/> has been disposed.</exception>
    public void Acknowledge()
    {
        if (_acknowledgeCompletionSource.Task.IsCompleted)
        {
            // Already acknowledged
            return;
        }

        _manager.Untrack(this);
        _acknowledgeCompletionSource.TrySetResult();
    }

    internal void SetResult(HttpResponseMessage result)
    {
        SetProgress(1);
        SetState(BackgroundHttpRequestState.Completed);
        _manager.Save(this);
        _resultCompletionSource.TrySetResult(result);
    }

    internal void SetCanceled()
    {
        SetProgress(1);
        SetState(BackgroundHttpRequestState.Canceled);
        _manager.Save(this);
    }

    internal void UpdateProgress(double progress)
    {
        SetProgress((float)progress);
        if (State != BackgroundHttpRequestState.Running)
        {
            SetState(BackgroundHttpRequestState.Running);
        }

        _manager.Save(this);
    }

    internal void SetPaused()
    {
        SetState(BackgroundHttpRequestState.Canceled);
        _manager.Save(this);
    }

    internal void SetRunning()
    {
        SetState(BackgroundHttpRequestState.Running);
        _manager.Save(this);
    }

    internal void SetError(HttpRequestException httpRequestException)
    {
        SetState(BackgroundHttpRequestState.Failed);
        _manager.Save(this);
        _resultCompletionSource.TrySetException(httpRequestException);
    }

    private void SetProgress(float progress)
    {
        _descriptor.Progress = progress;
        OnPropertyChanged(nameof(Progress));
    }

    private void SetState(BackgroundHttpRequestState state)
    {
        _descriptor.State = state;
        OnPropertyChanged(nameof(State));
    }
}
