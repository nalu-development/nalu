namespace Nalu;

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0290

/// <summary>
/// Represents an HTTP request message that can be sent by a <see cref="BackgroundHttpClient"/>.
/// </summary>
public class BackgroundHttpRequestMessage : HttpRequestMessage
{
    /// <summary>
    /// Gets the unique name of the request.
    /// </summary>
    public string RequestName { get; }

    /// <summary>
    /// Gets or sets the human-readable description of the request.
    /// </summary>
    public string? UserDescription { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundHttpRequestMessage"/> class with a <see cref="Guid"/> as request name.
    /// </summary>
    public BackgroundHttpRequestMessage()
        : this(Guid.NewGuid().ToString())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundHttpRequestMessage"/> class.
    /// </summary>
    /// <param name="requestName">The unique name of the request.</param>
    public BackgroundHttpRequestMessage(string requestName)
        : this(requestName, HttpMethod.Get, (Uri?)null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundHttpRequestMessage"/> class.
    /// </summary>
    /// <param name="requestName">The unique name of the request.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="requestUri">The relative or absolute URI.</param>
    public BackgroundHttpRequestMessage(string requestName, HttpMethod method, Uri? requestUri)
        : base(method, requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestName);
        RequestName = requestName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundHttpRequestMessage"/> class.
    /// </summary>
    /// <param name="requestName">The unique name of the request.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="requestUri">The relative or absolute URI.</param>
    public BackgroundHttpRequestMessage(string requestName, HttpMethod method, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri)
        : this(requestName, method, string.IsNullOrEmpty(requestUri) ? null : new Uri(requestUri, UriKind.RelativeOrAbsolute))
    {
    }

    internal Uri GetRequestUri(BackgroundHttpClient client) =>
        RequestUri?.IsAbsoluteUri == true
            ? RequestUri
            : client.BaseAddress is null
                ? throw new InvalidOperationException("The request URI is not an absolute URI and no base address is set.")
                : RequestUri is null
                    ? client.BaseAddress
                    : new Uri(client.BaseAddress, RequestUri);
}
