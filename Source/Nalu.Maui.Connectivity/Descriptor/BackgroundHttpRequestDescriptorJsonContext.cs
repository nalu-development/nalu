namespace Nalu;

using System.Text.Json.Serialization;

/// <summary>
/// JsonSerializer context for <see cref="BackgroundHttpRequestDescriptor"/>.
/// </summary>
[JsonSerializable(typeof(BackgroundHttpRequestDescriptor))]
internal partial class BackgroundHttpRequestDescriptorJsonContext : JsonSerializerContext;
