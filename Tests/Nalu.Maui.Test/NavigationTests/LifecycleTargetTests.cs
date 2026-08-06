namespace Nalu.Maui.Test.NavigationTests;

#pragma warning disable CA2012, CS4014, VSTHRD200

public class LifecycleTargetTests
{
    private sealed class ViewOnlyGuardPage : ContentPage, ILeavingGuard
    {
        public int CanLeaveCalls { get; private set; }

        public bool CanLeaveResult { get; set; }

        public ValueTask<bool> CanLeaveAsync()
        {
            ++CanLeaveCalls;

            return ValueTask.FromResult(CanLeaveResult);
        }
    }

    [Fact(DisplayName = "GetLifecycleTarget, when no binding context is set, should return the page itself")]
    public void GetLifecycleTargetWhenNoBindingContextIsSetShouldReturnThePageItself()
    {
        var page = new ContentPage();

        NavigationHelper.GetLifecycleTarget(page).Should().BeSameAs(page);
    }

    [Fact(DisplayName = "GetLifecycleTarget, when a binding context is explicitly set, should return it even if the page implements lifecycle interfaces")]
    public void GetLifecycleTargetWhenABindingContextIsExplicitlySetShouldReturnIt()
    {
        var model = Substitute.For<ILeavingGuard>();

        var page = new ViewOnlyGuardPage
        {
            BindingContext = model
        };

        NavigationHelper.GetLifecycleTarget(page).Should().BeSameAs(model);
    }

    [Fact(DisplayName = "GetLifecycleTarget, when the binding context is inherited from the parent, should return the page itself")]
    public void GetLifecycleTargetWhenTheBindingContextIsInheritedShouldReturnThePageItself()
    {
        var host = new ContentPage
        {
            BindingContext = new object()
        };

        var page = new ContentPage();
        host.AddLogicalChild(page);

        // Sanity: inheritance is live, but it is not an explicit assignment.
        page.BindingContext.Should().BeSameAs(host.BindingContext);
        NavigationHelper.GetLifecycleTarget(page).Should().BeSameAs(page);
    }

    [Fact(DisplayName = "GetLifecycleTarget, when the page sets itself as binding context, should return the page")]
    public void GetLifecycleTargetWhenThePageSetsItselfAsBindingContextShouldReturnThePage()
    {
        var page = new ViewOnlyGuardPage();
        page.BindingContext = page;

        NavigationHelper.GetLifecycleTarget(page).Should().BeSameAs(page);
    }

    [Fact(DisplayName = "CanLeaveAsync, on a view-only guard page, should invoke the page's guard")]
    public async Task CanLeaveAsyncOnAViewOnlyGuardPageShouldInvokeThePagesGuard()
    {
        var shellProxy = Substitute.For<IShellProxy>();

        var page = new ViewOnlyGuardPage
        {
            CanLeaveResult = false
        };

        var canLeave = await NavigationHelper.CanLeaveAsync(shellProxy, page);

        canLeave.Should().BeFalse();
        page.CanLeaveCalls.Should().Be(1);
    }

    [Fact(DisplayName = "CanLeaveAsync, when both page and explicit binding context implement the guard, should only invoke the binding context")]
    public async Task CanLeaveAsyncWhenBothImplementTheGuardShouldOnlyInvokeTheBindingContext()
    {
        var shellProxy = Substitute.For<IShellProxy>();
        var model = Substitute.For<ILeavingGuard>();
        model.CanLeaveAsync().Returns(new ValueTask<bool>(true));

        var page = new ViewOnlyGuardPage
        {
            CanLeaveResult = false,
            BindingContext = model
        };

        var canLeave = await NavigationHelper.CanLeaveAsync(shellProxy, page);

        canLeave.Should().BeTrue();
        model.Received(1).CanLeaveAsync();
        page.CanLeaveCalls.Should().Be(0);
    }
}
