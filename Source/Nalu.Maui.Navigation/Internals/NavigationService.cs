using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Nalu.Internals;

namespace Nalu;

#pragma warning disable IDE0290

internal class NavigationService : INavigationService, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<StrongBox<bool>> _isNavigating = new();
    private readonly LeakDetector? _leakDetector;
    private readonly TimeProvider _timeProvider;
    private readonly ICommand _backCommand;
    private IShellProxy? _shellProxy;

    public IShellProxy ShellProxy => _shellProxy ?? throw new InvalidOperationException("You must use NaluShell to navigate with INavigationService.");
    public INavigationConfiguration Configuration { get; }

    internal IServiceProvider ServiceProvider { get; }

    public NavigationService(INavigationConfiguration configuration, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILogger<NavigationService>>(); // TODO: add log messages all around in this class

        Configuration = configuration;
        ServiceProvider = serviceProvider;
        _timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        _backCommand = new Command(NavigateBack);

        var trackLeaks
            = (Configuration.LeakDetectorState == NavigationLeakDetectorState.EnabledWithDebugger && Debugger.IsAttached) ||
              Configuration.LeakDetectorState == NavigationLeakDetectorState.Enabled;

        _leakDetector = trackLeaks ? new LeakDetector() : null;

        void NavigateBack()
        {
            var popNavigation = Navigation.Relative().Pop();
            GoToAsync(popNavigation).FireAndForget(logger);
        }
    }

    public void Dispose()
    {
        _leakDetector?.Dispose();
        _semaphore.Dispose();
    }

    public async Task InitializeAsync(IShellProxy shellProxy, string contentSegmentName, object? intent)
    {
        _shellProxy = shellProxy;
        _shellProxy.InitializeWithContent(contentSegmentName);

        var content = _shellProxy.GetContent(contentSegmentName);
        var page = content.GetOrCreateContent();

        NavigationHelper.AssertIntent(page, intent, content.DestroyContent);

        var enteringTask = NavigationHelper.SendEnteringAsync(ShellProxy, page, intent, Configuration).AsTask();

        if (!enteringTask.IsCompleted)
        {
            throw new NotSupportedException($"OnEnteringAsync() must not be async for the initial page {page.BindingContext!.GetType().FullName}.");
        }

#pragma warning disable VSTHRD002
        // Rethrow eventual exceptions
        await enteringTask.ConfigureAwait(true);
#pragma warning restore VSTHRD002

        await NavigationHelper.SendAppearingAsync(ShellProxy, page, intent, Configuration).ConfigureAwait(true);
    }

    public async Task<bool> GoToAsync(INavigationInfo navigation)
    {
        if (navigation.Count == 0)
        {
            throw new InvalidNavigationException("Navigation must contain at least one segment.");
        }

        var disposeBag = new HashSet<object>();
        var shellProxy = ShellProxy;

        return await ExecuteNavigationAsync(
                navigation,
                async initialState =>
                {
                    if (navigation.Behavior?.HasFlag(NavigationBehavior.Immediate) != true)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(60), _timeProvider).ConfigureAwait(true);
                    }

                    var (requestedNavigation, targetState) = ComputeNavigationState(Configuration, navigation, initialState);

                    shellProxy.SendNavigationLifecycleEvent(
                        new NavigationLifecycleEventArgs(
                            NavigationLifecycleEventType.NavigationRequested,
                            new NavigationLifecycleInfo(navigation, requestedNavigation, targetState, initialState)
                        )
                    );

                    // Propose the navigation to the shell
                    if (!shellProxy.ProposeNavigation(navigation))
                    {
                        shellProxy.SendNavigationLifecycleEvent(
                            new NavigationLifecycleEventArgs(
                                NavigationLifecycleEventType.NavigationCanceled,
                                new NavigationLifecycleInfo(navigation, requestedNavigation, targetState, initialState)
                            )
                        );

                        return false;
                    }

                    shellProxy.BeginNavigation();

                    try
                    {
                        var ignoreGuards = navigation.Behavior?.HasFlag(NavigationBehavior.IgnoreGuards) ?? false;

                        var result = await (navigation switch
                        {
                            { IsAbsolute: true } => ExecuteAbsoluteNavigationAsync(navigation, disposeBag, ignoreGuards).ConfigureAwait(true),
                            _ => ExecuteRelativeNavigationAsync(navigation, disposeBag, ignoreGuards: ignoreGuards).ConfigureAwait(true)
                        });

                        var navigationLifecycleEventType = result ? NavigationLifecycleEventType.NavigationCompleted : NavigationLifecycleEventType.NavigationCanceled;

                        shellProxy.SendNavigationLifecycleEvent(
                            new NavigationLifecycleEventArgs(
                                navigationLifecycleEventType,
                                new NavigationLifecycleInfo(navigation, requestedNavigation, targetState, shellProxy.State)
                            )
                        );

                        return result;
                    }
                    catch (Exception ex)
                    {
                        shellProxy.SendNavigationLifecycleEvent(
                            new NavigationLifecycleEventArgs(
                                NavigationLifecycleEventType.NavigationFailed,
                                new NavigationLifecycleInfo(navigation, requestedNavigation, targetState, shellProxy.State),
                                data: ex
                            )
                        );

                        throw;
                    }
                    finally
                    {
                        await shellProxy.CommitNavigationAsync(() =>
                                            {
                                                foreach (var toDispose in disposeBag)
                                                {
                                                    DisposeElement(toDispose);
                                                }
                                            }
                                        )
                                        .ConfigureAwait(true);
                    }
                }
            )
            .ConfigureAwait(true);
    }

    internal Page CreatePage(Type pageType, Page? parentPage)
    {
        var serviceScope = ServiceProvider.CreateScope();
        
        var navigationServiceProvider = serviceScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();

        if (parentPage is not null && PageNavigationContext.Get(parentPage) is { ServiceScope: { } parentScope })
        {
            var parentNavigationServiceProvider = parentScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            navigationServiceProvider.SetParent(parentNavigationServiceProvider);
        }

        var page = (Page) serviceScope.ServiceProvider.GetRequiredService(pageType);
        navigationServiceProvider.SetContextPage(page);

        var isRoot = parentPage is null;
        ConfigureBackButtonBehavior(page, isRoot);

        var pageContext = new PageNavigationContext(serviceScope);

        PageNavigationContext.Set(page, pageContext);

        return page;
    }

    private void ConfigureBackButtonBehavior(Page page, bool isRoot)
    {
        var backButtonImage = isRoot ? Configuration.MenuImage : Configuration.BackImage;

        if (backButtonImage is null && isRoot)
        {
            return;
        }

        var backButtonBehavior = Shell.GetBackButtonBehavior(page);

        if (backButtonBehavior is null)
        {
            backButtonBehavior = new BackButtonBehavior();
            Shell.SetBackButtonBehavior(page, backButtonBehavior);
        }

        if (backButtonImage is not null)
        {
            backButtonBehavior.IconOverride ??= backButtonImage;
        }

        if (!isRoot)
        {
            backButtonBehavior.Command ??= _backCommand;
        }
    }

    private async Task<bool> ExecuteRelativeNavigationAsync(
        INavigationInfo navigation,
        HashSet<object> disposeBag,
        IShellSectionProxy? section = null,
        List<NavigationStackPage>? stack = null,
        bool sendAppearingToTarget = true,
        bool ignoreGuards = false,
        Func<Task>? onCheckingGuardAsync = null
    )
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

                    await NavigationHelper.SendAppearingAsync(ShellProxy, stackPage.Page, null, Configuration).ConfigureAwait(true);
                    var canLeave = await NavigationHelper.CanLeaveAsync(ShellProxy, stackPage.Page).ConfigureAwait(true);

                    if (!canLeave)
                    {
                        return false;
                    }
                }

                await NavigationHelper.SendDisappearingAsync(ShellProxy, stackPage.Page).ConfigureAwait(true);
                await NavigationHelper.SendLeavingAsync(ShellProxy, stackPage.Page).ConfigureAwait(true);

                stack.RemoveAt(stack.Count - 1);
                await shellProxy.PopAsync(section).ConfigureAwait(true);

                disposeBag.Add(stackPage.Page);
            }
            else
            {
                await NavigationHelper.SendDisappearingAsync(ShellProxy, stackPage.Page).ConfigureAwait(true);
                var pageType = NavigationHelper.GetPageType(segment.Type, Configuration);
                var segmentName = segment.SegmentName ?? NavigationSegmentAttribute.GetSegmentName(pageType);

                var page = CreatePage(pageType, stackPage.Page);

                var isModal = Shell.GetPresentationMode(page).HasFlag(PresentationMode.Modal);
                await NavigationHelper.SendEnteringAsync(ShellProxy, page, intent, Configuration).ConfigureAwait(true);
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
            await NavigationHelper.SendAppearingAsync(ShellProxy, page, intent, Configuration).ConfigureAwait(true);
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

                contentsToLeave =
                [
                    ..currentItem
                      .Sections
                      .SelectMany(section => section.Contents)
                      .Where(content => content.Page is not null)
                      .OrderByDescending(content => content.Parent == currentSection)
                      .ThenBy(content => content.Parent.SegmentName)
                      .ThenByDescending(content => content == currentContent)
                ];
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

                contentsToLeave =
                [
                    ..currentSection
                      .Contents
                      .Where(content => content.Page is not null)
                      .OrderByDescending(content => content == currentContent)
                ];
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
        Destroy
    }

    private async Task<bool> ExecuteCrossContentNavigationAsync(
        INavigationInfo navigation,
        HashSet<object> disposeBag,
        IList<IShellContentProxy> contentsToLeave,
        IShellContentProxy targetContent,
        ContentLeaveMode leaveMode,
        bool ignoreGuards
    )
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
                    await NavigationHelper.SendDisappearingAsync(ShellProxy, navigationStack[^1].Page).ConfigureAwait(true);

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
                                EnsureContentIsSelectedAsync
                            )
                            .ConfigureAwait(true))
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

                    await NavigationHelper.SendAppearingAsync(ShellProxy, contentPage, null, Configuration).ConfigureAwait(true);

                    if (!await NavigationHelper.CanLeaveAsync(ShellProxy, contentPage).ConfigureAwait(true))
                    {
                        return false;
                    }
                }

                await NavigationHelper.SendDisappearingAsync(ShellProxy, contentPage).ConfigureAwait(true);
                await NavigationHelper.SendLeavingAsync(ShellProxy, contentPage).ConfigureAwait(true);

                if (leaveMode == ContentLeaveMode.Destroy)
                {
                    disposeBag.Add(contentToLeave);
                }

                Task EnsureContentIsSelectedAsync()
                {
                    return shellProxy.CurrentItem.CurrentSection.CurrentContent == contentToLeave
                        ? Task.CompletedTask
                        : ShellProxy.SelectContentAsync(contentToLeave.SegmentName);
                }
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
        await NavigationHelper.SendEnteringAsync(ShellProxy, targetContentPage, intent, Configuration).ConfigureAwait(true);
        await ShellProxy.SelectContentAsync(targetContent.SegmentName).ConfigureAwait(true);

        var targetSection = targetContent.Parent;
        var targetStack = targetSection.GetNavigationStack(targetContent).ToList();
        var relativeNavigation = ToRelativeNavigation(navigation, targetStack);

        if (relativeNavigation.Count > 0)
        {
            var result = await ExecuteRelativeNavigationAsync(relativeNavigation, disposeBag, targetSection, targetStack, ignoreGuards: ignoreGuards).ConfigureAwait(true);
            await shellProxy.CommitNavigationAsync().ConfigureAwait(true);

            return result;
        }

        await shellProxy.CommitNavigationAsync().ConfigureAwait(true);
        await NavigationHelper.SendAppearingAsync(ShellProxy, targetContentPage, intent, Configuration).ConfigureAwait(true);

        return true;
    }

    private RelativeNavigation ToRelativeNavigation(INavigationInfo navigation, IReadOnlyList<NavigationStackPage> navigationStackPages)
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
            ((IList<INavigationSegment>) relativeNavigation).Add(navigation[i]);
        }

        if (navigation.Intent is { } intent && relativeNavigation.Count > 0)
        {
            relativeNavigation.WithIntent(intent);
        }

        return relativeNavigation;
    }

    private async Task<bool> ExecuteNavigationAsync(INavigationInfo navigation, Func<string, Task<bool>> navigationFunc)
    {
        if (_isNavigating.Value is { Value: true })
        {
            throw new InvalidNavigationException("Cannot trigger a navigation from within a navigation, try to use IDispatcher.DispatchDelayed.");
        }

        var shellProxy = ShellProxy;
        var initialState = shellProxy.State;
        var initialLocation = shellProxy.Location;

        await _semaphore.WaitAsync().ConfigureAwait(true);

        var currentLocation = shellProxy.Location;

        if (initialLocation != currentLocation)
        {
            var currentState = shellProxy.State;
            // State has changed, abort the navigation
            var (requestedNavigation, targetState) = ComputeNavigationState(Configuration, navigation, initialState);

            shellProxy.SendNavigationLifecycleEvent(
                new NavigationLifecycleEventArgs(
                    NavigationLifecycleEventType.NavigationIgnored,
                    new NavigationLifecycleInfo(navigation, requestedNavigation, targetState, currentState)
                )
            );

            _semaphore.Release();

            return false;
        }

        _isNavigating.Value = new StrongBox<bool>(true);

        try
        {
            var result = await navigationFunc(initialState).ConfigureAwait(true);

            return result;
        }
        finally
        {
            _semaphore.Release();

            _isNavigating.Value.Value = false;
        }
    }

    private void DisposeElement(object toDispose)
    {
        switch (toDispose)
        {
            case Page page:
            {
                DisconnectHandlerHelper.DisconnectHandlers(page);
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

    private static (string RequestedNavigation, string TargetState) ComputeNavigationState(INavigationConfiguration configuration, INavigationInfo navigation, string initialState)
    {
        string requestedNavigation;
        string targetState;

        var segments = navigation.Select(s => s.Type != null ? NavigationHelper.GetPageType(s.Type, configuration).Name : s.SegmentName).ToArray();

        if (navigation.IsAbsolute)
        {
            requestedNavigation = targetState = $"//{string.Join('/', segments)}";
        }
        else
        {
            var currentSegments = initialState.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var popCount = segments.Count(segment => segment == NavigationPop.PopRoute);
            var maintainedSegmentsCount = currentSegments.Length - popCount;
            var pushBase = maintainedSegmentsCount > 0 ? currentSegments.Take(maintainedSegmentsCount) : [];
            requestedNavigation = $"{string.Join('/', segments)}";
            targetState = $"//{string.Join('/', pushBase.Concat(segments.Skip(popCount)))}";
        }

        if (navigation.Intent is { } intent)
        {
            requestedNavigation += $"?Intent={intent.GetType().Name}";
        }

        return (requestedNavigation, targetState);
    }
}
