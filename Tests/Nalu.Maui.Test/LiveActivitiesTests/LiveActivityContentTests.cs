namespace Nalu.Maui.Test.LiveActivitiesTests;

public class LiveActivityContentTests
{
    private static LiveActivityContent CreateContent() => new()
    {
        Title = "Pizza on the way",
        Subtitle = "Preparing",
        ChipText = "10%",
        AccentColor = "#4C7DF0",
        Progress = new LiveActivityProgress
        {
            Value = 0.1,
            Segments = [new LiveActivityProgressSegment { Weight = 2, Color = "#FF0000" }, new LiveActivityProgressSegment()],
            Points = [new LiveActivityProgressPoint { Position = 0.5 }]
        },
        Timer = LiveActivityTimer.CountDown(DateTimeOffset.FromUnixTimeMilliseconds(1_000_000)),
        Actions = [new LiveActivityAction { Label = "Track", DeepLink = "nalu://track" }],
        Custom = new Dictionary<string, string> { ["k"] = "v" }
    };

    [Fact(DisplayName = "DeepClone produces an equal but fully isolated copy")]
    public void DeepCloneProducesAnEqualButFullyIsolatedCopy()
    {
        var content = CreateContent();

        var clone = content.DeepClone();

        // Record equality is reference-based on collection properties, so the payload —
        // which is also what the update pipeline dedupes on — is the equality that matters.
        LiveActivityContentSerializer.Serialize(clone).Should().Be(LiveActivityContentSerializer.Serialize(content));

        // Mutating any nested object of the clone must not leak into the original.
        clone.Progress!.Value = 0.9;
        clone.Progress.Segments![0].Color = "#00FF00";
        clone.Progress.Points![0].Position = 0.7;
        clone.Timer!.EndsAt = DateTimeOffset.FromUnixTimeMilliseconds(2_000_000);
        clone.Actions![0].Label = "Changed";
        clone.Custom!["k"] = "changed";

        content.Progress!.Value.Should().Be(0.1);
        content.Progress.Segments![0].Color.Should().Be("#FF0000");
        content.Progress.Points![0].Position.Should().Be(0.5);
        content.Timer!.EndsAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1_000_000));
        content.Actions![0].Label.Should().Be("Track");
        content.Custom!["k"].Should().Be("v");
    }

    [Fact(DisplayName = "Serializer round-trips the full content model")]
    public void SerializerRoundTripsTheFullContentModel()
    {
        var content = CreateContent();

        var json = LiveActivityContentSerializer.Serialize(content);
        var roundTripped = LiveActivityContentSerializer.Deserialize(json);

        roundTripped.Should().NotBeNull();
        LiveActivityContentSerializer.Serialize(roundTripped!).Should().Be(json);
        roundTripped!.Progress!.Segments.Should().HaveCount(2);
        roundTripped.Timer!.Mode.Should().Be(LiveActivityTimerMode.CountDown);
        roundTripped.Actions.Should().ContainSingle(a => a.DeepLink == "nalu://track");
    }

    [Fact(DisplayName = "Serializer uses the camelCase widget contract and omits nulls")]
    public void SerializerUsesTheCamelCaseWidgetContractAndOmitsNulls()
    {
        var json = LiveActivityContentSerializer.Serialize(new LiveActivityContent { Title = "T", ChipText = "5 min" });

        json.Should().Contain("\"title\":\"T\"");
        json.Should().Contain("\"chipText\":\"5 min\"");
        json.Should().NotContain("subtitle", "null properties are omitted from the payload");
    }

    [Fact(DisplayName = "Deserialize returns null for malformed payloads")]
    public void DeserializeReturnsNullForMalformedPayloads()
        => LiveActivityContentSerializer.Deserialize("{not json").Should().BeNull();
}
