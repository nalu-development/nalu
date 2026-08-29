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
    UseStringEnumConverter = true,
    Converters = [typeof(EpochMsDateTimeOffsetConverter), typeof(MsTimeSpanConverter)])]
[JsonSerializable(typeof(LiveActivityContent))]
internal sealed partial class LiveActivityJsonContext : JsonSerializerContext;

/// <summary>
/// Instants travel as epoch milliseconds: trivially consumed by the Swift widget
/// (<c>Date(timeIntervalSince1970:)</c>) and the Java notification renderer, unlike
/// .NET's 7-fractional-digit ISO strings.
/// </summary>
internal sealed class EpochMsDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
}

/// <summary>Durations travel as milliseconds; see <see cref="EpochMsDateTimeOffsetConverter"/>.</summary>
internal sealed class MsTimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TimeSpan.FromMilliseconds(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        => writer.WriteNumberValue((long)value.TotalMilliseconds);
}
