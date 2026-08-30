namespace Nalu.Maui.Test.LiveActivitiesTests;

public class TimerPayloadTests
{
    [Fact(DisplayName = "Timer instants travel as epoch milliseconds in the widget contract")]
    public void TimerInstantsTravelAsEpochMillisecondsInTheWidgetContract()
    {
        var startsAt = DateTimeOffset.FromUnixTimeMilliseconds(1_788_000_000_000);
        var endsAt = startsAt.AddMinutes(1);

        var json = LiveActivityContentSerializer.Serialize(new LiveActivityContent
        {
            Timer = LiveActivityTimer.CountDown(endsAt, startsAt)
        });

        json.Should().Be("""{"timer":{"mode":"CountDown","startsAt":1788000000000,"endsAt":1788000060000}}""");
    }
}
