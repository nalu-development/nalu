using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nalu;

/// <summary>
/// Serializes <see cref="LiveActivityContent"/> to the JSON contract shared with the
/// iOS widget (camelCase, nulls omitted). The same payload doubles as the no-op
/// detector (identical payload ⇒ update skipped) and as the persisted snapshot for
/// rehydration after process restarts.
/// </summary>
internal static class LiveActivityContentSerializer
{
    public static string Serialize(LiveActivityContent content)
        => JsonSerializer.Serialize(content, LiveActivityJsonContext.Default.LiveActivityContent);

    public static LiveActivityContent? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, LiveActivityJsonContext.Default.LiveActivityContent);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LiveActivityContent))]
internal sealed partial class LiveActivityJsonContext : JsonSerializerContext;
