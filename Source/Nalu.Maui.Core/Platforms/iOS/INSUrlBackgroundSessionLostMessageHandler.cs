namespace Nalu;

/// <summary>
/// A handler that is called when the app is being booted to handle response messages completed when the app was not running.
/// </summary>
/// <remarks>
/// Failed requests (throwing an exception) are not reported to this handler.
/// </remarks>
public interface INSUrlBackgroundSessionLostMessageHandler
{
    /// <summary>
    /// Handles result of a background request completed when the app was not running.
    /// </summary>
    /// <param name="responseHandle">The background response handle.</param>
    /// <remarks>
    /// This method is supposed to invoke <see cref="NSUrlBackgroundResponseHandle.GetResponseAsync()" />
    /// and dispose the provided <see cref="HttpResponseMessage" /> as soon as possible,
    /// but not before storing the result where appropriate.
    /// </remarks>
    Task HandleLostMessageAsync(NSUrlBackgroundResponseHandle responseHandle);
}
