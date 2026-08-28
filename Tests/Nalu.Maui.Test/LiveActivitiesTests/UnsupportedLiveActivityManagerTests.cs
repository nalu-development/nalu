namespace Nalu.Maui.Test.LiveActivitiesTests;

public class UnsupportedLiveActivityManagerTests
{
    [Fact(DisplayName = "Unsupported manager hands out inert but fully usable handles")]
    public async Task UnsupportedManagerHandsOutInertButFullyUsableHandles()
    {
        var manager = new UnsupportedLiveActivityManager();

        manager.Support.Should().Be(LiveActivitySupport.Unavailable);
        (await manager.RequestPermissionAsync()).Should().BeFalse();

        var content = new LiveActivityContent { Title = "A" };
        var activity = await manager.StartAsync("demo", content);

        // Intake clone: mutating the caller's instance after StartAsync is inert.
        content.Title = "mutated";
        activity.Content.Title.Should().Be("A");

        await activity.UpdateAsync(c => c.Title = "B");
        activity.Content.Title.Should().Be("B");

        await activity.EndAsync();
        activity.State.Should().Be(LiveActivityState.Ended);

        manager.Activities.Should().ContainSingle().Which.Kind.Should().Be("demo");
    }

    [Fact(DisplayName = "Options map kinds to display names with a pass-through default")]
    public void OptionsMapKindsToDisplayNamesWithAPassThroughDefault()
    {
        var options = new LiveActivityOptions().AddKind("demo", "Demo activities");

        options.GetKindDisplayName("demo").Should().Be("Demo activities");
        options.GetKindDisplayName("other").Should().Be("other");
    }
}
