namespace Nalu;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nalu.Internals;

#pragma warning disable IDE0290

internal partial class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<StrongBox<bool>> _isNavigating = new();
    private readonly LeakDetector? _leakDetector;
    private readonly TimeProvider _timeProvider;
    private IShellProxy? _shellProxy;

    public IShellProxy ShellProxy => _shellProxy ?? throw new InvalidOperationException("You must use NaluShell to navigate with INavigationService.");
    public INavigationConfiguration Configuration { get; }

    public NavigationService(INavigationConfiguration configuration, IServiceProvider serviceProvider)
    {
        Configuration = configuration;
        _serviceProvider = serviceProvider;
        _timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

        var trackLeaks
            = (Configuration.LeakDetectorState == NavigationLeakDetectorState.EnabledWithDebugger && Debugger.IsAttached) ||
              Configuration.LeakDetectorState == NavigationLeakDetectorState.Enabled;
        _leakDetector = trackLeaks ? new LeakDetector() : null;
    }

    public void Dispose()
    {
        _leakDetector?.Dispose();
        _semaphore.Dispose();
    }

    public async Task InitializeAsync(IShellProxy shellProxy, string contentSegmentName, object? intent)
    {
        _shellProxy = shellProxy;

        var content = _shellProxy.GetContent(contentSegmentName);
        var page = content.GetOrCreateContent();

        NavigationHelper.AssertIntent(page, intent, content.DestroyContent);

        var enteringTask = NavigationHelper.SendEnteringAsync(page, intent, Configuration).AsTask();
        if (!enteringTask.IsCompleted)
        {
            throw new NotSupportedException($"OnEnteringAsync() must not be async for the initial page {page.BindingContext!.GetType().FullName}.");
        }

#pragma warning disable VSTHRD002
        // Rethrow eventual exceptions
        await enteringTask.ConfigureAwait(true);
        await _shellProxy.SelectContentAsync(contentSegmentName).ConfigureAwait(true);
#pragma warning restore VSTHRD002

        await NavigationHelper.SendAppearingAsync(page, intent, Configuration).ConfigureAwait(true);
    }

    public async Task<bool> GoToAsync(INavigationInfo navigation)
    {
        if (navigation.Count == 0)
        {
            throw new InvalidNavigationException("Navigation must contain at least one segment.");
        }

        var disposeBag = new HashSet<object>();
        var shellProxy = ShellProxy;

        return await ExecuteNavigationAsync(async () =>
        {
            if (navigation.Behavior?.HasFlag(NavigationBehavior.Immediate) != true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(60), _timeProvider).ConfigureAwait(true);
            }

            shellProxy.BeginNavigation();
            try
            {
                var ignoreGuards = navigation.Behavior?.HasFlag(NavigationBehavior.IgnoreGuards) ?? false;
                return await (navigation switch
                {
                    { IsAbsolute: true } => ExecuteAbsoluteNavigationAsync(navigation, disposeBag, ignoreGuards).ConfigureAwait(true),
                    _ => ExecuteRelativeNavigationAsync(navigation, disposeBag, ignoreGuards: ignoreGuards).ConfigureAwait(true),
                });
            }
            finally
            {
                await shellProxy.CommitNavigationAsync(() =>
                {
                    foreach (var toDispose in disposeBag)
                    {
                        DisposePage(toDispose);
                    }
                }).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    internal Page CreatePage(Type pageType, Page? parentPage, ImageSource? backButtonImage = null)
    {
        var serviceScope = _serviceProvider.CreateScope();

        if (parentPage is not null && PageNavigationContext.Get(parentPage) is { ServiceScope: { } parentScope })
        {
            var parentNavigationServiceProvider = parentScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            var navigationServiceProvider = serviceScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            navigationServiceProvider.SetParent(parentNavigationServiceProvider);
        }

        var page = (Page)serviceScope.ServiceProvider.GetRequiredService(pageType);
        ConfigureBackButtonBehavior(page, backButtonImage);

        var pageContext = new PageNavigationContext(serviceScope);

        PageNavigationContext.Set(page, pageContext);

        return page;
    }

    internal static void ConfigureBackButtonBehavior(Page page, ImageSource? backButtonImage)
    {
        if (backButtonImage is null)
        {
            return;
        }

        var backButtonBehavior = Shell.GetBackButtonBehavior(page);
        if (backButtonBehavior is null)
        {
            backButtonBehavior = new BackButtonBehavior
            {
                IconOverride = backButtonImage,
            };
            Shell.SetBackButtonBehavior(page, backButtonBehavior);
        }
        else
        {
            backButtonBehavior.IconOverride = backButtonImage;
        }
    }

    private async Task<bool> ExecuteRelativeNavigationAsync(
        INavigationInfo navigation,
        HashSet<object> disposeBag,
        IShellSectionProxy? section = null,
        List<NavigationStackPage>? stack = null,
        bool sendAppearingToTarget = true,
        bool ignoreGuards = false,
        Func<Task>? onCheckingGuardAsync = null)
    {
        if (navigation.Count == 0)
        {
            return true;
        }

        var shellProxy = ShellProxy;
        section ??= shellProxy.CurrentItem.CurrentSection;
        stack ??= section.GetNavigationStack().ToList();

        var popCount = navigation.Count(segment => segment.SegmentName == NavigationPop.PopRoute);
        if (popCount >= stack.Count)
        {
            throw new InvalidNavigationException("Cannot pop more pages than the stack contains.");
        }

        var navigationCount = navigation.Count;
        for (var i = 0; i < navigationCount; i++)
        {
            var segment = navigation[i];
            var stackPage = stack[^1];
            var intent = i == navigationCount - 1 ? navigation.Intent : null;
            var isPop = segment.SegmentName == NavigationPop.PopRoute;
            if (isPop)
            {
                var isGuarded = !ignoreGuards && stackPage.Page.BindingContext is ILeavingGuard;
                if (isGuarded)
                {
                    if (onCheckingGuardAsync is not null)
                    {
                        await onCheckingGuardAsync().ConfigureAwait(true);
                    }

                    await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
                    shellProxy.BeginNavigation();

                    await NavigationHelper.SendAppearingAsync(stackPage.Page, null, Configuration).ConfigureAwait(true);
                    var canLeave = await NavigationHelper.CanLeaveAsync(stackPage.Page).ConfigureAwait(true);
                    if (!canLeave)
                    {
                        return false;
                    }
                }

                await NavigationHelper.SendDisappearingAsync(stackPage.Page).ConfigureAwait(true);
                await NavigationHelper.SendLeavingAsync(stackPage.Page).ConfigureAwait(true);

                stack.RemoveAt(stack.Count - 1);
                await shellProxy.PopAsync(section).ConfigureAwait(true);

                disposeBag.Add(stackPage.Page);
            }
            else
            {
                await NavigationHelper.SendDisappearingAsync(stackPage.Page).ConfigureAwait(true);
                var pageType = NavigationHelper.GetPageType(segment.Type, Configuration);
                var segmentName = segment.SegmentName ?? NavigationSegmentAttribute.GetSegmentName(pageType);

                var page = CreatePage(pageType, stackPage.Page, Configuration.BackImage);

                var isModal = Shell.GetPresentationMode(page).HasFlag(PresentationMode.Modal);
                await NavigationHelper.SendEnteringAsync(page, intent, Configuration).ConfigureAwait(true);
                await shellProxy.PushAsync(segmentName, page).ConfigureAwait(true);
                stack.Add(new NavigationStackPage($"{stackPage.Route}/{segmentName}", segmentName, page, isModal));
            }
        }

        if (sendAppearingToTarget)
        {
            var page = stack[^1].Page;
            var intent = navigation.Intent;

            await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
            shellProxy.BeginNavigation();

            NavigationHelper.AssertIntent(page, intent);
            await NavigationHelper.SendAppearingAsync(page, intent, Configuration).ConfigureAwait(true);
        }

        return true;
    }

    private async Task<bool> ExecuteAbsoluteNavigationAsync(INavigationInfo navigation, HashSet<object> disposeBag, bool ignoreGuards)
    {
        var behavior = navigation.Behavior ?? NavigationBehavior.PopAllPagesOnItemChange;
        var shellProxy = ShellProxy;
        var rootSegment = navigation[0];
        var rootSegmentName = NavigationHelper.GetSegmentName(rootSegment, Configuration);

        // Get current navigation stack
        var currentItem = shellProxy.CurrentItem;
        var currentSection = currentItem.CurrentSection;
        var currentContent = currentSection.CurrentContent;
        var navigationStack = currentSection.GetNavigationStack().ToList();
        var rootStackPage = navigationStack[0];
        if (rootStackPage.SegmentName == rootSegmentName)
        {
            // If we're in the same ShellContent, this is just a relative navigation
            var relativeNavigation = ToRelativeNavigation(navigation, navigationStack);
            return await ExecuteRelativeNavigationAsync(relativeNavigation, disposeBag, currentSection, navigationStack, ignoreGuards: ignoreGuards).ConfigureAwait(true);
        }

        var modalPages = navigationStack.Count(page => page.IsModal);
        if (modalPages > 0)
        {
            var popModalsNavigation = PopTimes(modalPages);
            if (!await ExecuteRelativeNavigationAsync(popModalsNavigation, disposeBag, currentSection, navigationStack, false, ignoreGuards).ConfigureAwait(true))
            {
                return false;
            }

            await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
            shellProxy.BeginNavigation();
        }

        var targetContent = shellProxy.GetContent(rootSegmentName);
        var targetSection = targetContent.Parent;
        var targetItem = targetSection.Parent;

        IList<IShellContentProxy> contentsToLeave;
        ContentLeaveMode leaveMode;

        if (targetItem != currentItem)
        {
            if (behavior.HasFlag(NavigationBehavior.PopAllPagesOnItemChange))
            {
                leaveMode = ContentLeaveMode.Destroy;
                contentsToLeave = [..currentItem
                    .Sections
                    .SelectMany(section => section.Contents)
                    .Where(content => content.Page is not null)
                    .OrderByDescending(content => content.Parent == currentSection)
                    .ThenBy(content => content.Parent.SegmentName)
                    .ThenByDescending(content => content == currentContent)];
            }
            else
            {
                leaveMode = ContentLeaveMode.None;
                contentsToLeave = [currentContent];
            }
        }
        else if (targetSection != currentSection)
        {
            if (behavior.HasFlag(NavigationBehavior.PopAllPagesOnSectionChange))
            {
                leaveMode = ContentLeaveMode.Destroy;
                contentsToLeave = [..currentSection
                    .Contents
                    .Where(content => content.Page is not null)
                    .OrderByDescending(content => content == currentContent)];
            }
            else
            {
                leaveMode = ContentLeaveMode.None;
                contentsToLeave = [currentContent];
            }
        }
        else
        {
            leaveMode = ContentLeaveMode.ClearStack;
            contentsToLeave = [currentContent];
        }

        return await ExecuteCrossContentNavigationAsync(navigation, disposeBag, contentsToLeave, targetContent, leaveMode, ignoreGuards).ConfigureAwait(true);
    }

    private enum ContentLeaveMode
    {
        None,
        ClearStack,
        Destroy,
    }

    private async Task<bool> ExecuteCrossContentNavigationAsync(
        INavigationInfo navigation,
        HashSet<object> disposeBag,
        IList<IShellContentProxy> contentsToLeave,
        IShellContentProxy targetContent,
        ContentLeaveMode leaveMode,
        bool ignoreGuards)
    {
        var shellProxy = ShellProxy;
        shellProxy.BeginNavigation();

        var groupedContentsToLeave = contentsToLeave.GroupBy(content => content.Parent).ToList();
        foreach (var sectionContentsToLeave in groupedContentsToLeave)
        {
            foreach (var contentToLeave in sectionContentsToLeave)
            {
                var navigationStack = contentToLeave.Parent.GetNavigationStack(contentToLeave).ToList();

                if (leaveMode == ContentLeaveMode.None)
                {
                    await EnsureContentIsSelectedAsync().ConfigureAwait(true);
                    await NavigationHelper.SendDisappearingAsync(navigationStack[^1].Page).ConfigureAwait(true);
                    continue;
                }

                var popCount = navigationStack.Count - 1;
                if (popCount > 0)
                {
                    var popNavigation = PopTimes(popCount);
                    if (!await ExecuteRelativeNavigationAsync(
                            popNavigation,
                            disposeBag,
                            contentToLeave.Parent,
                            navigationStack,
                            false,
                            ignoreGuards,
                            EnsureContentIsSelectedAsync).ConfigureAwait(true))
                    {
                        return false;
                    }
                }

                var contentPage = contentToLeave.Page!;
                if (!ignoreGuards && contentToLeave.HasGuard)
                {
                    await EnsureContentIsSelectedAsync().ConfigureAwait(true);

                    await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
                    shellProxy.BeginNavigation();

                    await NavigationHelper.SendAppearingAsync(contentPage, null, Configuration).ConfigureAwait(true);
                    if (!await NavigationHelper.CanLeaveAsync(contentPage).ConfigureAwait(true))
                    {
                        return false;
                    }
                }

                await NavigationHelper.SendDisappearingAsync(contentPage).ConfigureAwait(true);
                await NavigationHelper.SendLeavingAsync(contentPage).ConfigureAwait(true);

                if (leaveMode == ContentLeaveMode.Destroy)
                {
                    disposeBag.Add(contentToLeave);
                }

                Task EnsureContentIsSelectedAsync()
                    => shellProxy.CurrentItem.CurrentSection.CurrentContent == contentToLeave
                        ? Task.CompletedTask
                        : ShellProxy.SelectContentAsync(contentToLeave.SegmentName);
            }

            if (leaveMode == ContentLeaveMode.Destroy)
            {
                var sectionToLeave = sectionContentsToLeave.Key;
                if (sectionToLeave != targetContent.Parent)
                {
                    disposeBag.Add(sectionToLeave);
                }
            }
        }

        // Send entering
        var targetContentPage = targetContent.GetOrCreateContent();
        var targetIsShellContent = navigation.Count == 1;
        var intent = targetIsShellContent ? navigation.Intent : null;
        await NavigationHelper.SendEnteringAsync(targetContentPage, intent, Configuration).ConfigureAwait(true);
        await ShellProxy.SelectContentAsync(targetContent.SegmentName).ConfigureAwait(true);

        if (!targetIsShellContent)
        {
            var targetSection = targetContent.Parent;
            var targetStack = targetSection.GetNavigationStack(targetContent).ToList();
            var relativeNavigation = ToRelativeNavigation(navigation, targetStack);
            var result = await ExecuteRelativeNavigationAsync(relativeNavigation, disposeBag, targetSection, targetStack, ignoreGuards: ignoreGuards).ConfigureAwait(true);
            await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
            return result;
        }

        await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
        await NavigationHelper.SendAppearingAsync(targetContentPage, intent, Configuration).ConfigureAwait(true);
        return true;
    }

    private RelativeNavigation ToRelativeNavigation(INavigationInfo navigation, IList<NavigationStackPage> navigationStackPages)
    {
        var matchingSegmentsCount = navigation
            .Skip(1)
            .Select(segment => NavigationHelper.GetSegmentName(segment, Configuration))
            .Zip(navigationStackPages.Skip(1).Select(stackPage => stackPage.SegmentName), (s1, s2) => (s1, s2))
            .TakeWhile(pair => pair.s1 == pair.s2)
            .Count();

        var relativeNavigation = new RelativeNavigation();

        // Add pop segments
        var popCount = navigationStackPages.Count - 1 - matchingSegmentsCount;
        while (popCount-- > 0)
        {
            relativeNavigation.Pop();
        }

        // Add push segments
        for (var i = 1 + matchingSegmentsCount; i < navigation.Count; i++)
        {
            ((IList<INavigationSegment>)relativeNavigation).Add(navigation[i]);
        }

        if (navigation.Intent is { } intent)
        {
            relativeNavigation.WithIntent(intent);
        }

        return relativeNavigation;
    }

    private async Task<bool> ExecuteNavigationAsync(Func<Task<bool>> navigationFunc)
    {
        if (_isNavigating.Value is { Value: true })
        {
            throw new InvalidNavigationException("Cannot trigger a navigation from within a navigation, try to use IDispatcher.DispatchDelayed.");
        }

        var shellProxy = ShellProxy;
        var initialState = shellProxy.State;

        await _semaphore.WaitAsync().ConfigureAwait(true);

        if (initialState != shellProxy.State)
        {
            // State has changed, abort the navigation
            _semaphore.Release();
            return false;
        }

        _isNavigating.Value = new StrongBox<bool>(true);

        try
        {
            var result = await navigationFunc().ConfigureAwait(true);
            return result;
        }
        finally
        {
            _semaphore.Release();

            _isNavigating.Value.Value = false;
        }
    }

    private void DisposePage(object toDispose)
    {
        switch (toDispose)
        {
            case Page page:
            {
                if (Environment.Version.Major < 9)
                {
                    DisconnectHandlerHelper.DisconnectHandlers(page);
                }

                PageNavigationContext.Dispose(page);
                _leakDetector?.Track(page);

                break;
            }

            case IShellContentProxy contentProxy:
            {
                var contentPage = contentProxy.Page;
                if (contentPage is not null)
                {
                    DisconnectHandlerHelper.DisconnectHandlers(contentPage);
                    contentProxy.DestroyContent();
                    _leakDetector?.Track(contentPage);
                }

                break;
            }

            case IShellSectionProxy sectionProxy:
            {
                sectionProxy.RemoveStackPages();
                break;
            }

            default:
            {
                throw new InvalidNavigationException("Trying to dispose an unknown object.");
            }
        }
    }

    private static INavigationInfo PopTimes(int popCount)
    {
        var navigation = Navigation.Relative();
        while (popCount-- > 0)
        {
            navigation.Pop();
        }

        return navigation;
    }
}
