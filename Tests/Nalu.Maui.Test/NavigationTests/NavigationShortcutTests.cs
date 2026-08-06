namespace Nalu.Maui.Test.NavigationTests;

public class NavigationShortcutTests
{
    private sealed class SomePage;

    private sealed class OtherPage;

    [Fact(DisplayName = "Navigation.Push<T>() should build the same navigation as Relative().Push<T>()")]
    public void PushShouldBuildTheSameNavigationAsRelativePush()
    {
        var shorthand = (INavigationInfo) Navigation.Push<SomePage>();
        var longForm = (INavigationInfo) Navigation.Relative().Push<SomePage>();

        shorthand.IsAbsolute.Should().BeFalse();
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Push<T>(intent) should end the chain carrying the intent")]
    public void PushWithIntentShouldEndTheChainCarryingTheIntent()
    {
        var shorthand = Navigation.Push<SomePage>(42);
        var longForm = Navigation.Relative().Push<SomePage>().WithIntent(42);

        shorthand.Intent.Should().Be(42);
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Push<T>() should stay chainable")]
    public void PushShouldStayChainable()
    {
        var shorthand = (INavigationInfo) Navigation.Push<SomePage>().Push<OtherPage>();
        var longForm = (INavigationInfo) Navigation.Relative().Push<SomePage>().Push<OtherPage>();

        shorthand.Count.Should().Be(2);
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Pop() should build the same navigation as Relative().Pop()")]
    public void PopShouldBuildTheSameNavigationAsRelativePop()
    {
        var shorthand = (INavigationInfo) Navigation.Pop();
        var longForm = (INavigationInfo) Navigation.Relative().Pop();

        shorthand.IsAbsolute.Should().BeFalse();
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Pop(intent) should end the chain carrying the intent for the revealed page")]
    public void PopWithIntentShouldEndTheChainCarryingTheIntent()
    {
        var shorthand = Navigation.Pop("result");
        var longForm = Navigation.Relative().Pop().WithIntent("result");

        shorthand.Intent.Should().Be("result");
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Pop() should stay chainable into multi-pop and pushes")]
    public void PopShouldStayChainable()
    {
        var shorthand = (INavigationInfo) Navigation.Pop().Pop().Push<OtherPage>();
        var longForm = (INavigationInfo) Navigation.Relative().Pop().Pop().Push<OtherPage>();

        shorthand.Count.Should().Be(3);
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Root<T>() should build the same navigation as Absolute().Root<T>()")]
    public void RootShouldBuildTheSameNavigationAsAbsoluteRoot()
    {
        var shorthand = (INavigationInfo) Navigation.Root<SomePage>();
        var longForm = (INavigationInfo) Navigation.Absolute().Root<SomePage>();

        shorthand.IsAbsolute.Should().BeTrue();
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Root<T>(intent) should end the chain carrying the intent")]
    public void RootWithIntentShouldEndTheChainCarryingTheIntent()
    {
        var shorthand = Navigation.Root<SomePage>(42);
        var longForm = Navigation.Absolute().Root<SomePage>().WithIntent(42);

        shorthand.Intent.Should().Be(42);
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Navigation.Root<T>() should stay chainable into stack pages")]
    public void RootShouldStayChainable()
    {
        var shorthand = (INavigationInfo) Navigation.Root<SomePage>().Add<OtherPage>();
        var longForm = (INavigationInfo) Navigation.Absolute().Root<SomePage>().Add<OtherPage>();

        shorthand.Count.Should().Be(2);
        shorthand.Matches(longForm).Should().BeTrue();
    }

    [Fact(DisplayName = "Shortcuts should not match navigations with different segments or intents")]
    public void ShortcutsShouldNotMatchDifferentNavigations()
    {
        ((INavigationInfo) Navigation.Push<SomePage>()).Matches(Navigation.Push<OtherPage>()).Should().BeFalse();
        Navigation.Push<SomePage>(42).Matches(Navigation.Push<SomePage>(43)).Should().BeFalse();
        Navigation.Push<SomePage>(42).Matches(Navigation.Push<SomePage>()).Should().BeFalse();
        ((INavigationInfo) Navigation.Root<SomePage>()).Matches(Navigation.Push<SomePage>()).Should().BeFalse();
    }
}
