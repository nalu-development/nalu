namespace Nalu.Maui.Test.Navigation;

using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;

public class NavigationTests
{
    private class SomePageModel : ObservableObject;

    [Fact(DisplayName = "Relative navigation, equals to another relative navigation with same segments")]
    public void RelativeNavigationEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        var r1 = Nalu.Navigation.Relative().Pop().Pop().Push<SomePageModel>();
        var r2 = Nalu.Navigation.Relative().Pop().Pop().Push<SomePageModel>();
        var r3 = Nalu.Navigation.Relative().Pop().Push<SomePageModel>();

        r1.Matches(r2).Should().BeTrue();
        r1.Matches(r3).Should().BeFalse();
    }

    [Fact(DisplayName = "Relative navigation with intent, equals to another relative navigation with same segments")]
    public void RelativeNavigationWithIntentEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        var intent = "Hello";
        var r1 = Nalu.Navigation.Relative().Push<SomePageModel>().WithIntent(intent);
        var r2 = Nalu.Navigation.Relative().Push<SomePageModel>().WithIntent(intent);
        var r3 = Nalu.Navigation.Relative().Push<SomePageModel>();

        r1.Matches(r2).Should().BeTrue();
        r1.Matches(r3).Should().BeFalse();
    }

    [Fact(DisplayName = "Absolute navigation, equals to another relative navigation with same segments")]
    public void AbsoluteNavigationEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        var a1 = Nalu.Navigation.Absolute().ShellContent<SomePageModel>();
        var a2 = Nalu.Navigation.Absolute().ShellContent<SomePageModel>();
        var a3 = Nalu.Navigation.Absolute().ShellContent<SomePageModel>().Add<SomePageModel>();

        a1.Matches(a2).Should().BeTrue();
        a1.Matches(a3).Should().BeFalse();
    }

    [Fact(DisplayName = "Absolute navigation with intent, equals to another relative navigation with same segments")]
    public void AbsoluteNavigationWithIntentEqualsToAnotherRelativeNavigationWithSameSegments()
    {
        const string intent = "Hello";
        var a1 = Nalu.Navigation.Absolute().ShellContent<SomePageModel>().WithIntent(intent);
        var a2 = Nalu.Navigation.Absolute().ShellContent<SomePageModel>().WithIntent(intent);
        var a3 = Nalu.Navigation.Absolute().ShellContent<SomePageModel>();

        a1.Matches(a2).Should().BeTrue();
        a1.Matches(a3).Should().BeFalse();
    }

    [Fact(DisplayName = "Relative navigation, when pop follows push, throws exception")]
    public void RelativeNavigationWhenPopFollowsPushThrowsException()
    {
        var r = (IRelativeNavigationInitialBuilder)Nalu.Navigation.Relative().Push<SomePageModel>();

        Action addPop = () => r.Pop();

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Relative navigation, when inserting push before pop, throws exception")]
    public void RelativeNavigationWhenInsertingPushBeforePopThrowsException()
    {
        var r = (Nalu.Navigation)Nalu.Navigation.Relative().Pop();

        var addPop = () => r.Insert(0, (NavigationSegment)typeof(SomePageModel));

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Relative navigation, when inserting pop after pop, throws exception")]
    public void RelativeNavigationWhenInsertingPopAfterPopThrowsException()
    {
        var r = (Nalu.Navigation)Nalu.Navigation.Relative().Push<SomePageModel>();

        var addPop = () => r.Insert(1, new NavigationPop());

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Absolute navigation, when inserting pop, throws exception")]
    public void AbsoluteNavigationWhenInsertingPopThrowsException()
    {
        var a = (Nalu.Navigation)Nalu.Navigation.Absolute();

        var addPop = () => a.Insert(0, new NavigationPop());

        addPop.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Absolute navigation, when adding pop, throws exception")]
    public void AbsoluteNavigationWhenAddingPopThrowsException()
    {
        var a = (Nalu.Navigation)Nalu.Navigation.Absolute();

        var addPop = () => a.Add(new NavigationPop());

        addPop.Should().Throw<InvalidOperationException>();
    }
}
