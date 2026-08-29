using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nalu;

/// <summary>
/// One entry of the native rehydration list: both bridges (the iOS ActivityKit bridge and
/// the Android notification layer) report their live activities as a JSON array of these.
/// </summary>
internal sealed record LiveActivityRehydrationInfo(string? Id, string? Kind, string? Payload, string? State);

/// <summary>
/// Parses the rehydration JSON handed over by the native layers.
/// </summary>
internal static class LiveActivityRehydration
{
    public static List<LiveActivityRehydrationInfo> Parse(string? json)
    {
        if (json is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, LiveActivityRehydrationJsonContext.Default.ListLiveActivityRehydrationInfo) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<LiveActivityRehydrationInfo>))]
internal sealed partial class LiveActivityRehydrationJsonContext : JsonSerializerContext;
