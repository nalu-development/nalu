
namespace Nalu.Maui.Test.NavigationTests;

#pragma warning disable CA2012,CS4014,VSTHRD110

public partial class NavigationServiceTests
{
    private NavigationService _navigationService = null!;
    private IShellProxy _shellProxy = null!;
    private ServiceProvider _serviceProvider = null!;
    private INavigationConfiguration _navigationConfiguration = null!;

    [Fact(DisplayName = "NavigationService, when initialized, should set the root page, sending entering and appearing")]
    public async Task NavigationServiceWhenInitializedShouldSetTheRootPageSendingEnteringAndAppearing()
    {
        const string segmentName = nameof(Page1);
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, segmentName, null);

        _shellProxy.Received().InitializeWithContent(segmentName);
        var page = _shellProxy.GetContent(segmentName).Page!;
        var model = (IPage1Model) page.BindingContext;

        Received.InOrder(
            () =>
            {
                model.OnEnteringAsync();
                model.OnAppearingAsync();
            }
        );

        model.DidNotReceive().OnEnteringAsync(null!);
        model.DidNotReceive().OnAppearingAsync(null!);
    }

    [Fact(DisplayName = "NavigationService, when initialized with intent, should set the root page, sending entering and appearing with intent")]
    public async Task NavigationServiceWhenInitializedWithIntentShouldSetTheRootPageSendingEnteringAndAppearingWithIntent()
    {
        const string segmentName = nameof(Page1);
        var intent = new OddIntent();
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, segmentName, intent);

        _shellProxy.Received().InitializeWithContent(segmentName);
        var page = _shellProxy.GetContent(segmentName).Page!;
        var model = (IPage1Model) page.BindingContext;

        Received.InOrder(
            () =>
            {
                model.OnEnteringAsync(intent);
                model.OnAppearingAsync(intent);
            }
        );

        model.DidNotReceive().OnEnteringAsync();
        model.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when initialized with incorrect intent, should throw")]
    public void NavigationServiceWhenInitializedWithIncorrectIntentShouldThrow()
    {
        const string segmentName = nameof(Page1);
        ConfigureTestAsync("c1");

        var initializeAction = () => _navigationService.InitializeAsync(_shellProxy, segmentName, "an unexpected intent");

        initializeAction.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "NavigationService, when pushing a page, should send disappearing on current page, then set the new page, sending entering and appearing")]
    public async Task NavigationServiceWhenPushingAPageShouldSendDisappearingOnCurrentPageThenSetTheNewPageSendingEnteringAndAppearing()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        var content1 = _shellProxy.GetContent(nameof(Page1));
        var page1 = content1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());

        var page2 = content1.Parent.GetNavigationStack().ElementAt(1).Page;
        var model2 = (IPage2Model) page2.BindingContext;

        Received.InOrder(
            () =>
            {
                model1.OnDisappearingAsync();
                model2.OnEnteringAsync();
                _shellProxy.PushAsync(nameof(Page2), page2);
                model2.OnAppearingAsync();
            }
        );

        model1.DidNotReceive().OnLeavingAsync();
        model2.DidNotReceive().OnEnteringAsync(null!);
        model2.DidNotReceive().OnAppearingAsync(null!);
    }

    [Fact(DisplayName = "NavigationService, when pushing a page with intent, should send disappearing on current page, then set the new page, sending entering and appearing")]
    public async Task NavigationServiceWhenPushingAPageWithIntentShouldSendDisappearingOnCurrentPageThenSetTheNewPageSendingEnteringAndAppearing()
    {
        var intent = new EvenIntent();
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        var content1 = _shellProxy.GetContent(nameof(Page1));
        var page1 = content1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>().WithIntent(intent));

        var page2 = content1.Parent.GetNavigationStack().ElementAt(1).Page;
        var model2 = (IPage2Model) page2.BindingContext;

        Received.InOrder(
            () =>
            {
                model1.OnDisappearingAsync();
                model2.OnEnteringAsync(intent);
                _shellProxy.PushAsync(nameof(Page2), page2);
                model2.OnAppearingAsync(intent);
            }
        );

        model1.DidNotReceive().OnLeavingAsync();
        model2.DidNotReceive().OnEnteringAsync();
        model2.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when pushing a page with incorrect intent, should throw")]
    public async Task NavigationServiceWhenPushingAPageWithIncorrectIntentShouldThrow()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        var pushAction = () => _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>().WithIntent("unexpected intent"));

        await pushAction.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "NavigationService, when popping a page, should not leak the popped page")]
    public async Task NavigationServiceWhenPoppingAPageShouldNotLeakThePoppedPage()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());

        WeakReference<Page> weakPage;

        {
            var shellContent = _shellProxy.GetContent(nameof(Page1));
            var shellSection = shellContent.Parent;
            var navigationStackPages = shellSection.GetNavigationStack().ToList();
            var page2 = navigationStackPages[1].Page;
            var model2 = (IPage2Model) page2.BindingContext;
            model2.ClearReceivedCalls();
            weakPage = new WeakReference<Page>(page2);
        }

        await _navigationService.GoToAsync(Navigation.Relative().Pop());
        _shellProxy.ClearReceivedCalls();

        await Task.Yield();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        weakPage.TryGetTarget(out _).Should().BeFalse();
    }

    [Fact(DisplayName = "NavigationService, when popping a page, should send disappearing and leaving on current page, then pop sending appearing")]
    public async Task NavigationServiceWhenPoppingAPageShouldSendDisappearingAndLeavingOnCurrentPageThenPopSendingAppearing()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Pop());

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnAppearingAsync();
                model2.Dispose();
            }
        );

        model1.DidNotReceive().OnEnteringAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a page with intent, should send disappearing and leaving on current page, then pop sending appearing")]
    public async Task NavigationServiceWhenPoppingAPageWithIntentShouldSendDisappearingAndLeavingOnCurrentPageThenPopSendingAppearing()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        var intent = new OddIntent();
        await _navigationService.GoToAsync(Navigation.Relative().Pop().WithIntent(intent));

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnAppearingAsync(intent);
                model2.Dispose();
            }
        );

        model1.DidNotReceive().OnEnteringAsync(intent);
        model1.DidNotReceive().OnEnteringAsync();
        model1.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a page with incorrect intent, should throw")]
    public async Task NavigationServiceWhenPoppingAPageWithIncorrectIntentShouldThrow()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        var popAction = () => _navigationService.GoToAsync(Navigation.Relative().Pop().WithIntent("Unexpected intent"));

        await popAction.Should().ThrowAsync<InvalidOperationException>();

        model1.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping multiple pages, should send disappearing only to current page and appearing to target page")]
    public async Task NavigationServiceWhenPoppingMultiplePagesShouldSendDisappearingOnlyToCurrentPageAndAppearingToTargetPage()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>().Push<IPage3Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;
        var page3 = navigationStackPages[2].Page;
        var model3 = (IPage3Model) page3.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        model3.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Pop().Pop());

        Received.InOrder(
            () =>
            {
                model3.OnDisappearingAsync();
                model3.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnAppearingAsync();
                model3.Dispose();
                model2.Dispose();
            }
        );

        model1.DidNotReceive().OnEnteringAsync();
        model2.DidNotReceive().OnAppearingAsync();
        model2.DidNotReceive().OnDisappearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a guarded page, evaluates guard and cancels if false")]
    public async Task NavigationServiceWhenPoppingAGuardedPageEvaluatesGuardAndCancelsIfFalse()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage9Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage9Model) page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        model2.CanLeaveAsync().Returns(ValueTask.FromResult(false));

        var navigatedToTarget = await _navigationService.GoToAsync(Navigation.Relative().Pop());

        navigatedToTarget.Should().BeFalse();

        model2.Received().CanLeaveAsync();
        model2.DidNotReceive().OnDisappearingAsync();
        model2.DidNotReceive().OnLeavingAsync();
        _shellProxy.DidNotReceive().PopAsync(shellSection);
    }

    [Fact(DisplayName = "NavigationService, when popping a guarded page, evaluates guard and proceeds if true")]
    public async Task NavigationServiceWhenPoppingAGuardedPageEvaluatesGuardAndProceedsIfTrue()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage9Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage9Model) page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        model2.CanLeaveAsync().Returns(ValueTask.FromResult(true));

        var navigatedToTarget = await _navigationService.GoToAsync(Navigation.Relative().Pop());

        navigatedToTarget.Should().BeTrue();

        Received.InOrder(
            () =>
            {
                model2.CanLeaveAsync();
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnAppearingAsync();
                model2.Dispose();
            }
        );
    }

    [Fact(DisplayName = "NavigationService, when popping a non-current guarded page, appears and evaluates guard")]
    public async Task NavigationServiceWhenPoppingANonCurrentGuardedPageAppearsAndEvaluatesGuard()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage9Model>().Push<IPage2Model>());

        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage9Model) page2.BindingContext;
        var page3 = navigationStackPages[2].Page;
        var model3 = (IPage2Model) page3.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        model3.ClearReceivedCalls();

        model2.CanLeaveAsync().Returns(ValueTask.FromResult(true));

        var navigatedToTarget = await _navigationService.GoToAsync(Navigation.Relative().Pop().Pop());

        navigatedToTarget.Should().BeTrue();

        Received.InOrder(
            () =>
            {
                model3.OnDisappearingAsync();
                model3.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model2.OnAppearingAsync();
                model2.CanLeaveAsync();
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnAppearingAsync();
                model3.Dispose();
                model2.Dispose();
            }
        );
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on same content, should act as relative navigation")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnSameContentShouldActAsRelativeNavigation()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent.Parent;
        var page1 = shellContent.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        model1.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage1Model>().Add<IPage9Model>().Add<IPage2Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage9Model) page2.BindingContext;
        var page3 = navigationStackPages[2].Page;
        var model3 = (IPage2Model) page3.BindingContext;

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage1Model>().Add<IPage9Model>());

        Received.InOrder(
            () =>
            {
                model2.OnEnteringAsync();
                _shellProxy.PushAsync(nameof(Page9), page2);
                model3.OnEnteringAsync();
                _shellProxy.PushAsync(nameof(Page2), page3);
                model3.OnAppearingAsync();
                model3.OnDisappearingAsync();
                model3.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model2.OnAppearingAsync();
                model3.Dispose();
            }
        );

        _shellProxy.DidNotReceive().SelectContentAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on same section, should pop pages and change shell content")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnSameSectionShouldPopPagesAndChangeShellContent()
    {
        ConfigureTestAsync("s1[c1,c5]");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;
        model2.ClearReceivedCalls();

        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
                model2.Dispose();
            }
        );
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation, can push on the new stack")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationCanPushOnTheNewStack()
    {
        ConfigureTestAsync("s1[c1,c5]");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());
        var navigationStackPages1 = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages1[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;
        model2.ClearReceivedCalls();

        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage5Model>().Add<IPage4Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;
        var navigationStackPages5 = shellSection.GetNavigationStack().ToList();
        var page4 = navigationStackPages5[1].Page;
        var model4 = (IPage4Model) page4.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model4.OnEnteringAsync();
                _shellProxy.PushAsync(nameof(Page4), page4);
                model4.OnAppearingAsync();
                model2.Dispose();
            }
        );
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on same item but different section, should keep navigation stack")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnSameItemButDifferentSectionShouldKeepNavigationStack()
    {
        ConfigureTestAsync("i1[s1[c1],s2[c5]]");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
            }
        );

        model1.DidNotReceive().OnLeavingAsync();
        model1.DidNotReceive().OnAppearingAsync();
        model1.DidNotReceive().OnDisappearingAsync();
        model1.DidNotReceive().Dispose();
        model2.DidNotReceive().OnLeavingAsync();
        model2.DidNotReceive().Dispose();
        _shellProxy.DidNotReceive().PopAsync(shellSection);
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on same item but different section, should pop modal pages")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnSameItemButDifferentSectionShouldPopModalPages()
    {
        ConfigureTestAsync("i1[s1[c1],s2[c5]]");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage7Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage7Model) page2.BindingContext;

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
                model2.Dispose();
            }
        );

        model1.DidNotReceive().OnLeavingAsync();
        model1.DidNotReceive().OnAppearingAsync();
        model1.DidNotReceive().OnDisappearingAsync();
        model1.DidNotReceive().Dispose();
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on same item but different section with PopAllPagesOnSectionChange, should pop navigation stack")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnSameItemButDifferentSectionWithPopAllPagesOnSectionChangeShouldPopNavigationStack()
    {
        ConfigureTestAsync("i1[s1[c1],s2[c5]]");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.PopAllPagesOnSectionChange).Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnLeavingAsync();
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
                model2.Dispose();
                model1.Dispose();
            }
        );
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on different item, should pop navigation stacks")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnDifferentItemShouldPopNavigationStacks()
    {
        ConfigureTestAsync("i1[c1,c2,c3],c5");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page2), null);
        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage3Model>());
        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage1Model>());
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellContent2 = _shellProxy.GetContent(nameof(Page2));
        var shellContent3 = _shellProxy.GetContent(nameof(Page3));
        var shellSection1 = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;
        var page2 = shellContent2.Page!;
        var model2 = (IPage2Model) page2.BindingContext;
        var page3 = shellContent3.Page!;
        var model3 = (IPage3Model) page3.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage4Model>());
        var navigationStackPages = shellSection1.GetNavigationStack().ToList();
        var page4 = navigationStackPages[1].Page;
        var model4 = (IPage4Model) page4.BindingContext;

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        model3.ClearReceivedCalls();
        model4.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model4.OnDisappearingAsync();
                model4.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection1);
                model1.OnLeavingAsync();
                model2.OnLeavingAsync();
                model3.OnLeavingAsync();
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
                model4.Dispose();
                model1.Dispose();
                model2.Dispose();
                model3.Dispose();
            }
        );
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation on different item without default behavior, should not pop navigation stacks")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationOnDifferentItemWithoutDefaultBehaviorShouldNotPopNavigationStacks()
    {
        ConfigureTestAsync("c1,c5");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage2Model) page2.BindingContext;

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.None).Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
            }
        );

        model1.DidNotReceive().OnLeavingAsync();
        model1.DidNotReceive().OnAppearingAsync();
        model1.DidNotReceive().OnDisappearingAsync();
        model1.DidNotReceive().Dispose();
        model2.DidNotReceive().OnLeavingAsync();
        model2.DidNotReceive().Dispose();
        _shellProxy.DidNotReceive().PopAsync(shellSection);
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation with IgnoreGuards, should ignore guards")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationWithIgnoreGuardsShouldIgnoreGuards()
    {
        ConfigureTestAsync("c1,c5");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage9Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage9Model) page2.BindingContext;
        model2.CanLeaveAsync().Returns(ValueTask.FromResult(false));

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.IgnoreGuards | NavigationBehavior.PopAllPagesOnItemChange).Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnLeavingAsync();
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
                model2.Dispose();
                model1.Dispose();
            }
        );

        model2.DidNotReceive().CanLeaveAsync();
    }

    [Fact(DisplayName = "NavigationService, when doing absolute navigation, evaluates guards")]
    public async Task NavigationServiceWhenDoingAbsoluteNavigationEvaluatesGuards()
    {
        ConfigureTestAsync("c1,c5");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        var shellContent1 = _shellProxy.GetContent(nameof(Page1));
        var shellSection = shellContent1.Parent;
        var page1 = shellContent1.Page!;
        var model1 = (IPage1Model) page1.BindingContext;

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage9Model>());
        var navigationStackPages = shellSection.GetNavigationStack().ToList();
        var page2 = navigationStackPages[1].Page;
        var model2 = (IPage9Model) page2.BindingContext;
        model2.CanLeaveAsync().Returns(ValueTask.FromResult(true));

        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();
        _shellProxy.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IPage5Model>());
        var shellContent5 = _shellProxy.GetContent(nameof(Page5));
        var page5 = shellContent5.Page!;
        var model5 = (IPage5Model) page5.BindingContext;

        Received.InOrder(
            () =>
            {
                model2.CanLeaveAsync();
                model2.OnDisappearingAsync();
                model2.OnLeavingAsync();
                _shellProxy.PopAsync(shellSection);
                model1.OnLeavingAsync();
                model5.OnEnteringAsync();
                _shellProxy.SelectContentAsync(nameof(Page5));
                model5.OnAppearingAsync();
                model2.Dispose();
                model1.Dispose();
            }
        );
    }

    public interface ISomeContext;

    [Fact(DisplayName = "NavigationService, when pushing a page, initialize the nested navigation service provider")]
    public async Task NavigationServiceWhenPushingAPageInitializeTheNestedNavigationServiceProvider()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        var content1 = _shellProxy.GetContent(nameof(Page1));
        var page1 = content1.Page!;
        var page1Nsp = PageNavigationContext.Get(page1).ServiceScope.ServiceProvider.GetRequiredService<INavigationServiceProvider>();
        var context = Substitute.For<ISomeContext>();
        page1Nsp.AddNavigationScoped(context);

        await _navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>());

        var page2 = content1.Parent.GetNavigationStack().ElementAt(1).Page;
        using var page2Nsp = PageNavigationContext.Get(page2).ServiceScope.ServiceProvider.GetRequiredService<INavigationServiceProvider>();

        context.Should().BeSameAs(page2Nsp.GetRequiredService<ISomeContext>());
    }

    [Fact(DisplayName = "NavigationService, GoToAsync, can be easily testable")]
    public async Task NavigationServiceGoToAsyncCanBeEasilyTestable()
    {
        var navigationService = Substitute.For<INavigationService>();
        navigationService.GoToAsync(Arg.Any<Navigation>()).Returns(Task.FromResult(true));

        {
            // Simulate what would be the code to be tested
            var intent = new OddIntent("hello");
            await navigationService.GoToAsync(Navigation.Relative().Push<IPage2Model>().WithIntent(intent));
        }

        // Assert that the code did what it was supposed to do
        var expectedNavigation = Navigation.Relative().Push<IPage2Model>().WithIntent(new OddIntent("hello"));
        await navigationService.Received().GoToAsync(Arg.Is<Navigation>(n => n.Matches(expectedNavigation)));
    }
}
