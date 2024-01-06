namespace Nalu.Maui.Test.Navigation;

using CommunityToolkit.Mvvm.ComponentModel;
using Navigation = Nalu.Navigation;

public class NavigationTests
{
    private class SomePageModel : ObservableObject;

    [Fact(DisplayName = "Relative navigation, equals to another relative navigation with same segments")]
    public void RelativeNavigationEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        var r1 = Navigation.Relative().Pop().Pop().Push<SomePageModel>();
        var r2 = Navigation.Relative().Pop().Pop().Push<SomePageModel>();
        var r3 = Navigation.Relative().Pop().Push<SomePageModel>();

        r1.Equals(r2).Should().BeTrue();
        r1.Equals(r3).Should().BeFalse();
    }

    [Fact(DisplayName = "Relative navigation with intent, equals to another relative navigation with same segments")]
    public void RelativeNavigationWithIntentEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        var intent = "Hello";
        var r1 = Navigation.Relative(intent).Push<SomePageModel>();
        var r2 = Navigation.Relative(intent).Push<SomePageModel>();
        var r3 = Navigation.Relative().Push<SomePageModel>();

        r1.Equals(r2).Should().BeTrue();
        r1.Equals(r3).Should().BeFalse();
    }

    [Fact(DisplayName = "Absolute navigation, equals to another relative navigation with same segments")]
    public void AbsoluteNavigationEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        var a1 = Navigation.Absolute().Add<SomePageModel>();
        var a2 = Navigation.Absolute().Add<SomePageModel>();
        var a3 = Navigation.Absolute().Add<SomePageModel>().Add<SomePageModel>();

        a1.Equals(a2).Should().BeTrue();
        a1.Equals(a3).Should().BeFalse();
    }

    [Fact(DisplayName = "Absolute navigation with intent, equals to another relative navigation with same segments")]
    public void AbsoluteNavigationWithIntentEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        const string intent = "Hello";
        var a1 = Navigation.Absolute(intent).Add<SomePageModel>();
        var a2 = Navigation.Absolute(intent).Add<SomePageModel>();
        var a3 = Navigation.Absolute().Add<SomePageModel>();

        a1.Equals(a2).Should().BeTrue();
        a1.Equals(a3).Should().BeFalse();
    }

    [Fact(DisplayName = "Navigation equality, can be tested easily in navigation tests")]
    public void NavigationEqualityCanBeTestedEasilyInNavigationTests()
    {
        var navigationService = Substitute.For<INavigationService>();

        ActionToBeTested();

        _ = navigationService.Received().GoToAsync(Navigation.Relative().Pop());
        _ = navigationService.DidNotReceive().GoToAsync(Navigation.Relative("Some intent").Pop());

#pragma warning disable VSTHRD110
        void ActionToBeTested() => navigationService.GoToAsync(Navigation.Relative().Pop());
#pragma warning restore VSTHRD110
    }

    [Fact(DisplayName = "Relative navigation, when pop follows push, throws exception")]
    public void RelativeNavigationWhenPopFollowsPushThrowsException()
    {
        var r = Navigation.Relative().Push<SomePageModel>();

        Action addPop = () => r.Pop();

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Relative navigation, when inserting push before pop, throws exception")]
    public void RelativeNavigationWhenInsertingPushBeforePopThrowsException()
    {
        var r = Navigation.Relative().Pop();

        var addPop = () => r.Insert(0, new NavigationSegment<SomePageModel>());

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Relative navigation, when inserting pop after pop, throws exception")]
    public void RelativeNavigationWhenInsertingPopAfterPopThrowsException()
    {
        var r = Navigation.Relative().Push<SomePageModel>();

        var addPop = () => r.Insert(1, new NavigationPop());

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Absolute navigation, when inserting pop, throws exception")]
    public void AbsoluteNavigationWhenInsertingPopThrowsException()
    {
        var a = Navigation.Absolute();

        var addPop = () => a.Insert(0, new NavigationPop());

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Absolute navigation, when adding pop, throws exception")]
    public void AbsoluteNavigationWhenAddingPopThrowsException()
    {
        var a = Navigation.Absolute();

        var addPop = () => a.Add(new NavigationPop());

        addPop.Should().Throw<InvalidOperationException>();
    }
}
