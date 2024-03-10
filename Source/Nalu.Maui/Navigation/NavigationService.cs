namespace Nalu;

using System.Diagnostics;

#pragma warning disable IDE0290

internal class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly LeakDetector _leakDetector;
    private IShellProxy? _shellProxy;

    public IShellProxy ShellProxy => _shellProxy ?? throw new InvalidOperationException("You must use NaluShell to navigate with INavigationService.");
    public INavigationConfiguration Configuration { get; }

    public NavigationService(INavigationConfiguration configuration, IServiceProvider serviceProvider)
    {
        Configuration = configuration;
        _serviceProvider = serviceProvider;
        _leakDetector = new LeakDetector();
    }

    public void Dispose()
    {
        _leakDetector.Dispose();
        _semaphore.Dispose();
    }

    public async Task InitializeAsync(IShellProxy shellProxy, string contentSegmentName, object? intent)
    {
        _shellProxy = shellProxy;

        var content = _shellProxy.GetContent(contentSegmentName);
        var page = content.GetOrCreateContent();

        NavigationHelper.AssertIntent(page, intent, content.DestroyContent);

        var enteringTask = NavigationHelper.SendEnteringAsync(page, intent).AsTask();
        if (!enteringTask.IsCompleted)
        {
            throw new NotSupportedException($"OnEnteringAsync() must not be async for the initial page {page.BindingContext!.GetType().FullName}.");
        }

#pragma warning disable VSTHRD002
        // Rethrow eventual exceptions
        await enteringTask.ConfigureAwait(true);
        await _shellProxy.SelectContentAsync(contentSegmentName).ConfigureAwait(true);
#pragma warning restore VSTHRD002

        await NavigationHelper.SendAppearingAsync(page, intent).ConfigureAwait(true);
    }

    public Page CreatePage(Type pageType, Page? parentPage)
    {
        var serviceScope = _serviceProvider.CreateScope();
        var page = (Page)serviceScope.ServiceProvider.GetRequiredService(pageType);

        ConfigureBackButtonBehavior(page);

        if (parentPage is not null && PageNavigationContext.Get(parentPage) is { ServiceScope: { } parentScope })
        {
            var parentNavigationServiceProvider = parentScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            var navigationServiceProvider = serviceScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            navigationServiceProvider.SetParent(parentNavigationServiceProvider);
        }

        var pageContext = new PageNavigationContext(serviceScope);

        PageNavigationContext.Set(page, pageContext);

        return page;
    }

    public Task<bool> GoToAsync(INavigationInfo navigation)
    {
        if (navigation.Count == 0)
        {
            throw new InvalidOperationException("Navigation must contain at least one segment.");
        }

        return ExecuteNavigationAsync(() => navigation switch
        {
            { IsAbsolute: true } => ExecuteAbsoluteNavigationAsync(navigation),
            _ => ExecuteRelativeNavigationAsync(navigation),
        });
    }

    private void ConfigureBackButtonBehavior(Page page)
    {
        var backButtonBehavior = Shell.GetBackButtonBehavior(page);
        if (backButtonBehavior is not null)
        {
            backButtonBehavior.Command ??= new Command(() => _ = GoToAsync(Navigation.Relative().Pop()));
            backButtonBehavior.IconOverride ??= WithColor(Configuration.BackImage, ShellProxy.GetForegroundColor(page));
        }
        else
        {
            backButtonBehavior = new BackButtonBehavior
            {
                Command = new Command(() => _ = GoToAsync(Navigation.Relative().Pop())),
                IconOverride = WithColor(Configuration.BackImage, ShellProxy.GetForegroundColor(page)),
            };
            Shell.SetBackButtonBehavior(page, backButtonBehavior);
        }
    }

    private async Task<bool> ExecuteRelativeNavigationAsync(
        INavigationInfo navigation,
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
            throw new InvalidOperationException("Cannot pop more pages than the stack contains.");
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

                    await NavigationHelper.SendAppearingAsync(stackPage.Page, null).ConfigureAwait(true);
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

                PageNavigationContext.Dispose(stackPage.Page);
                if (Debugger.IsAttached)
                {
                    _leakDetector.Track(stackPage.Page);
                }
            }
            else
            {
                await NavigationHelper.SendDisappearingAsync(stackPage.Page).ConfigureAwait(true);
                var pageType = NavigationHelper.GetPageType(segment.Type, Configuration);
                var segmentName = segment.SegmentName ?? NavigationSegmentAttribute.GetSegmentName(pageType);

                var page = CreatePage(pageType, stackPage.Page);

                var isModal = Shell.GetPresentationMode(page).HasFlag(PresentationMode.Modal);
                await NavigationHelper.SendEnteringAsync(page, intent).ConfigureAwait(true);
                await shellProxy.PushAsync(segmentName, page).ConfigureAwait(true);
                stack.Add(new NavigationStackPage($"{stackPage.Route}/{segmentName}", segmentName, page, isModal));
            }
        }

        if (sendAppearingToTarget)
        {
            var page = stack[^1].Page;
            var intent = navigation.Intent;
            NavigationHelper.AssertIntent(page, intent);
            await NavigationHelper.SendAppearingAsync(page, intent).ConfigureAwait(true);
        }

        return true;
    }

    private async Task<bool> ExecuteAbsoluteNavigationAsync(INavigationInfo navigation)
    {
        var behavior = navigation.Behavior ?? NavigationBehavior.PopAllPagesOnItemChange;
        var ignoreGuards = behavior.HasFlag(NavigationBehavior.IgnoreGuards);
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
            return await ExecuteRelativeNavigationAsync(relativeNavigation, currentSection, navigationStack, ignoreGuards: ignoreGuards).ConfigureAwait(true);
        }

        var modalPages = navigationStack.Count(page => page.IsModal);
        if (modalPages > 0)
        {
            var popModalsNavigation = PopTimes(modalPages);
            if (!await ExecuteRelativeNavigationAsync(popModalsNavigation, currentSection, navigationStack, false, ignoreGuards).ConfigureAwait(true))
            {
                return false;
            }
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
                    .ThenBy(content => content.Parent)
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

        return await ExecuteCrossContentNavigationAsync(navigation, contentsToLeave, targetContent, leaveMode, ignoreGuards).ConfigureAwait(true);
    }

    private enum ContentLeaveMode
    {
        None,
        ClearStack,
        Destroy,
    }

    private async Task<bool> ExecuteCrossContentNavigationAsync(
        INavigationInfo navigation,
        IList<IShellContentProxy> contentsToLeave,
        IShellContentProxy targetContent,
        ContentLeaveMode leaveMode,
        bool ignoreGuards)
    {
        var shellProxy = ShellProxy;

        foreach (var content in contentsToLeave)
        {
            var navigationStack = content.Parent.GetNavigationStack().ToList();

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
                        content.Parent,
                        navigationStack,
                        false,
                        ignoreGuards,
                        EnsureContentIsSelectedAsync).ConfigureAwait(true))
                {
                    return false;
                }
            }

            var contentPage = content.Page!;
            if (!ignoreGuards && content.HasGuard)
            {
                await EnsureContentIsSelectedAsync().ConfigureAwait(true);
                await NavigationHelper.SendAppearingAsync(contentPage, null).ConfigureAwait(true);
                if (!await NavigationHelper.CanLeaveAsync(contentPage).ConfigureAwait(true))
                {
                    return false;
                }
            }

            await NavigationHelper.SendDisappearingAsync(contentPage).ConfigureAwait(true);
            await NavigationHelper.SendLeavingAsync(contentPage).ConfigureAwait(true);

            Task EnsureContentIsSelectedAsync()
                => shellProxy.CurrentItem.CurrentSection.CurrentContent == content
                    ? Task.CompletedTask
                    : ShellProxy.SelectContentAsync(content.SegmentName);
        }

        // Send entering
        var targetContentPage = targetContent.GetOrCreateContent();
        var targetIsShellContent = navigation.Count == 1;
        var intent = targetIsShellContent ? navigation.Intent : null;
        await NavigationHelper.SendEnteringAsync(targetContentPage, intent).ConfigureAwait(true);
        await ShellProxy.SelectContentAsync(targetContent.SegmentName).ConfigureAwait(true);

        if (leaveMode == ContentLeaveMode.Destroy)
        {
            foreach (var content in contentsToLeave)
            {
                if (Debugger.IsAttached)
                {
                    _leakDetector.Track(content.Page!);
                }

                content.DestroyContent();
            }
        }

        if (!targetIsShellContent)
        {
            var targetSection = targetContent.Parent;
            var targetStack = targetSection.GetNavigationStack().ToList();
            var relativeNavigation = ToRelativeNavigation(navigation, targetStack);
            return await ExecuteRelativeNavigationAsync(relativeNavigation, targetSection, targetStack, ignoreGuards: ignoreGuards).ConfigureAwait(true);
        }

        await NavigationHelper.SendAppearingAsync(targetContentPage, intent).ConfigureAwait(true);
        return true;
    }

    private RelativeNavigation ToRelativeNavigation(IReadOnlyList<INavigationSegment> navigation, IList<NavigationStackPage> navigationStackPages)
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

        return relativeNavigation;
    }

    private async Task<bool> ExecuteNavigationAsync(Func<Task<bool>> navigationFunc)
    {
        var taken = await _semaphore.WaitAsync(0).ConfigureAwait(true);
        if (!taken)
        {
            throw new InvalidOperationException("Cannot navigate while another navigation is in progress, try to use IDispatcher.DispatchDelayed.");
        }

        try
        {
            var result = await navigationFunc().ConfigureAwait(true);
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static ImageSource WithColor(ImageSource source, Color color)
    {
        if (source is FontImageSource fontSource)
        {
            var clone = new FontImageSource
            {
                Glyph = fontSource.Glyph,
                FontFamily = fontSource.FontFamily,
                Size = fontSource.Size,
                Color = color,
            };

            return clone;
        }

        return source;
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
