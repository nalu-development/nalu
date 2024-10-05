namespace Nalu;

/// <summary>
/// Represents the state of a background HTTP request.
/// </summary>
public enum BackgroundHttpRequestState
{
    /// <summary>
    /// The request is queued.
    /// </summary>
    Queued,

    /// <summary>
    /// The request is queued.
    /// </summary>
    Running,

    /// <summary>
    /// The request is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// The request has completed.
    /// </summary>
    /// <remarks>
    /// Request may have completed with a success or failure status code.
    /// </remarks>
    Completed,

    /// <summary>
    /// The request has been canceled.
    /// </summary>
    Canceled,

    /// <summary>
    /// The request has failed.
    /// </summary>
    Failed,
}
