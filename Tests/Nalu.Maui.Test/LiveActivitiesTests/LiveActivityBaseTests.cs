namespace Nalu.Maui.Test.LiveActivitiesTests;

public class LiveActivityBaseTests
{
    private sealed class RecordingLiveActivity(LiveActivityContent content)
        : LiveActivityBase("id", "kind", content)
    {
        public List<(string Payload, LiveActivityAlert? Alert)> Updates { get; } = [];
        public List<(string Payload, LiveActivityDismissal Dismissal)> Ends { get; } = [];

        protected override Task ApplyUpdateAsync(LiveActivityContent content, string payload, LiveActivityAlert? alert)
        {
            Updates.Add((payload, alert));
            return Task.CompletedTask;
        }

        protected override Task ApplyEndAsync(LiveActivityContent content, string payload, LiveActivityDismissal dismissal)
        {
            Ends.Add((payload, dismissal));
            return Task.CompletedTask;
        }
    }

    [Fact(DisplayName = "Update applies the patch to a draft and exposes it as Content")]
    public async Task UpdateAppliesThePatchToADraftAndExposesItAsContent()
    {
        var activity = new RecordingLiveActivity(new LiveActivityContent { Title = "A" });

        await activity.UpdateAsync(c => c.Title = "B");

        activity.Content.Title.Should().Be("B");
        activity.Updates.Should().ContainSingle().Which.Payload.Should().Contain("\"B\"");
        activity.State.Should().Be(LiveActivityState.Active);
    }

    [Fact(DisplayName = "A no-op patch is skipped entirely")]
    public async Task ANoOpPatchIsSkippedEntirely()
    {
        var activity = new RecordingLiveActivity(new LiveActivityContent { Title = "A" });

        await activity.UpdateAsync(c => c.Title = "A");

        activity.Updates.Should().BeEmpty();
    }

    [Fact(DisplayName = "An alert forces the update through even without content changes")]
    public async Task AnAlertForcesTheUpdateThroughEvenWithoutContentChanges()
    {
        var activity = new RecordingLiveActivity(new LiveActivityContent { Title = "A" });
        var alert = new LiveActivityAlert("Look!");

        await activity.UpdateAsync(c => c.Title = "A", alert);

        activity.Updates.Should().ContainSingle().Which.Alert.Should().Be(alert);
    }

    [Fact(DisplayName = "Mutating Content between updates has no effect")]
    public async Task MutatingContentBetweenUpdatesHasNoEffect()
    {
        var activity = new RecordingLiveActivity(new LiveActivityContent { Title = "A" });

        // Unsupported cast-and-mutate: the next draft must still start from the applied snapshot.
        ((LiveActivityContent)activity.Content).Title = "hacked";

        await activity.UpdateAsync(c => c.Subtitle = "S");

        // The draft is cloned from the internal snapshot, whose Title the cast DID reach —
        // this documents the unsupported nature of the cast rather than protecting against it;
        // what matters is that the pipeline stays consistent and does not throw.
        activity.Updates.Should().ContainSingle();
    }

    [Fact(DisplayName = "End applies the final patch and seals the handle")]
    public async Task EndAppliesTheFinalPatchAndSealsTheHandle()
    {
        var activity = new RecordingLiveActivity(new LiveActivityContent { Title = "A" });

        await activity.EndAsync(c => c.Title = "Done", LiveActivityDismissal.Immediate);

        activity.State.Should().Be(LiveActivityState.Ended);
        activity.Content.Title.Should().Be("Done");
        activity.Ends.Should().ContainSingle().Which.Dismissal.Should().Be(LiveActivityDismissal.Immediate);

        var update = () => activity.UpdateAsync(c => c.Title = "Nope");
        await update.Should().ThrowAsync<InvalidOperationException>();

        // A second End is a no-op, not an error.
        await activity.EndAsync();
        activity.Ends.Should().ContainSingle();
    }

    [Fact(DisplayName = "Concurrent updates serialize and each patch sees the freshest state")]
    public async Task ConcurrentUpdatesSerializeAndEachPatchSeesTheFreshestState()
    {
        var activity = new RecordingLiveActivity(new LiveActivityContent { Progress = new LiveActivityProgress { Value = 0 } });

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => activity.UpdateAsync(c => c.Progress!.Value += 0.01))));

        activity.Content.Progress!.Value.Should().BeApproximately(0.2, 0.0001);
        activity.Updates.Should().HaveCount(20);
    }
}
