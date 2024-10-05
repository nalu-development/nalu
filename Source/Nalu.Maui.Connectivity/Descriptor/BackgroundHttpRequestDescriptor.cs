namespace Nalu;

using System.Net;

internal class BackgroundHttpRequestDescriptor
{
    public required long RequestId { get; init; }
    public required string RequestName { get; init; }
    public bool IsMultiPart { get; init; }
    public string? UserDescription { get; set; }
    public float Progress { get; set; }
    public BackgroundHttpRequestState State { get; set; }
    public HttpStatusCode? ResponseStatusCode { get; set; }
    public Dictionary<string, string>? ResponseHeaders { get; set; }
}
