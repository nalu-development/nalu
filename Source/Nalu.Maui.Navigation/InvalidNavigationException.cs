namespace Nalu;

/// <summary>
/// Represents an exception thrown when triggering a navigation that cannot be performed.
/// </summary>
public class InvalidNavigationException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException);
