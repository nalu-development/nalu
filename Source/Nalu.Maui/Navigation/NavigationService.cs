namespace Nalu;

using System.ComponentModel;

internal sealed class NavigationService(IServiceProvider serviceProvider, INavigationOptions navigationOptions) : INavigationServiceInternal
{
    private IShellNavigationController? _controller;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<INotifyPropertyChanged> PageModelStack { get; private set; } = [];

    private IShellNavigationController Controller => _controller ?? throw new InvalidOperationException(
        "Navigation service must be initialized via UseNaluNavigation<TPageModel> on your Application or FlyoutPage");

    public Task<bool> GoToAsync(Navigation navigation)
    {
        if (navigation.Count == 0)
        {
            throw new InvalidOperationException("Navigation must contain at least one segment.");
        }

        var controller = Controller;

        return controller.ExecuteNavigationAsync(() => navigation switch
        {
            AbsoluteNavigation absoluteNavigation => PerformAbsoluteNavigationAsync(controller, absoluteNavigation),
            RelativeNavigation relativeNavigation => PerformRelativeNavigationAsync(controller, relativeNavigation),
            _ => throw new NotSupportedException($"{navigation.GetType().FullName} navigation type not supported."),
        });
    }

    async Task INavigationServiceInternal.InitializeAsync<TPageModel>(IShellNavigationController controller, object? intent)
    {
        if (_controller is not null)
        {
            throw new InvalidOperationException("Navigation service cannot be initialized twice.");
        }

        _controller = controller;

        NavigationHelper.AssertIntentAware(typeof(TPageModel), intent);
        var segmentName = NavigationHelper.GetSegmentName(typeof(TPageModel));
        var page = controller.GetRootPage(segmentName)
            ?? throw new InvalidOperationException($"Unable to find shell content for {typeof(TPageModel).FullName}.");

        var enteringTask = NavigationHelper.SendEnteringAsync(page, intent).AsTask();
        if (!enteringTask.IsCompleted)
        {
            throw new NotSupportedException($"OnEnteringAsync() must not be async for the initial page {page.BindingContext.GetType().FullName}.");
        }
#pragma warning disable VSTHRD002
        // Rethrow eventual exceptions
        await enteringTask.ConfigureAwait(true);
        await controller.SetRootPageAsync(segmentName).ConfigureAwait(true);
#pragma warning restore VSTHRD002

        _ = SendAppearingAndUpdateStackAsync(intent).AsTask();
    }

    Page INavigationServiceInternal.CreatePage(Type pageModelType) => CreatePage(pageModelType, null);

    private Page CreatePage(Type pageModelType, Page? parentPage)
    {
        if (!navigationOptions.Mapping.TryGetValue(pageModelType, out var pageType))
        {
            throw new InvalidOperationException($"No pages registered for {pageModelType.FullName}");
        }

        var serviceScope = serviceProvider.CreateScope();
        var page = (Page)serviceScope.ServiceProvider.GetRequiredService(pageType);

        Controller.ConfigurePage(page);

        if (parentPage is not null && PageNavigationContext.Get(parentPage) is { ServiceScope: { } parentScope })
        {
            var parentNavigationServiceProvider = parentScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            var navigationServiceProvider = serviceScope.ServiceProvider.GetRequiredService<INavigationServiceProviderInternal>();
            navigationServiceProvider.SetParent(parentNavigationServiceProvider);
        }

        if (page.BindingContext?.GetType().IsAssignableTo(pageModelType) != true)
        {
            throw new InvalidOperationException($"{page.GetType().FullName} must have a ${pageModelType.FullName} as BindingContext.");
        }

        var pageContext = new PageNavigationContext(serviceScope);

        PageNavigationContext.Set(page, pageContext);

        return page;
    }

    private async Task<bool> PerformAbsoluteNavigationAsync(IShellNavigationController controller, AbsoluteNavigation navigation)
    {
        var targetSegment = navigation[^1];
        var intent = navigation.Intent;
        NavigationHelper.AssertIntentAware(targetSegment.PageModelType, intent);

        var navigationStack = controller.NavigationStack
            .Select(page => page.BindingContext?.GetType())
            .ToList();

        var matchingSegmentsCount = navigation
            .Select(segment => segment.PageModelType)
            .Zip(navigationStack, (segmentPageModelType, navigationStackPageModelType) => (segmentPageModelType, navigationStackPageModelType))
            .TakeWhile(tuple => tuple.segmentPageModelType == tuple.navigationStackPageModelType)
            .Count();

        if (matchingSegmentsCount == navigation.Count && matchingSegmentsCount == navigationStack.Count)
        {
            throw new InvalidOperationException("Cannot navigate to the current page.");
        }

        var popCount = navigationStack.Count - matchingSegmentsCount;

        var relativeNavigation = Navigation.Relative(navigation.Intent);
        relativeNavigation.AddRange(Enumerable.Range(0, popCount).Select(_ => new NavigationPop()));
        relativeNavigation.AddRange(navigation.Skip(matchingSegmentsCount));

        return await PerformRelativeNavigationAsync(controller, relativeNavigation).ConfigureAwait(true);
    }

    private async Task<bool> PerformRelativeNavigationAsync(IShellNavigationController controller, RelativeNavigation navigation)
    {
        var navigationStack = controller.NavigationStack;

        var actions = GetRelativeNavigationActions(navigationStack, navigation);
        foreach (var action in actions)
        {
            var result = await (action.Action switch
            {
                NavigationAction.Pop => ExecutePopActionAsync(controller, action),
                NavigationAction.Push => ExecutePushActionAsync(controller, action),
                NavigationAction.ReplaceRoot => ExecuteSetRootActionAsync(controller, action),
                _ => throw new NotSupportedException($"{action.Action} navigation action not supported."),
            }).ConfigureAwait(true);

            if (!result)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ExecutePopActionAsync(IShellNavigationController controller, NavigationActionDescriptor actionDescriptor)
    {
        var page = actionDescriptor.Page!;
        var pageModel = page.BindingContext!;
        var flags = actionDescriptor.Flags;
        var sendDisappearing = flags.HasFlag(NavigationActionFlags.SendDisappearing);
        var intent = actionDescriptor.Intent;

        if (flags.HasFlag(NavigationActionFlags.Guarded))
        {
            var canLeave = await ((ILeavingGuard)pageModel).CanLeaveAsync().ConfigureAwait(true);
            if (!canLeave)
            {
                if (!sendDisappearing)
                {
                    await SendAppearingAndUpdateStackAsync(intent).ConfigureAwait(true);
                }

                return false;
            }
        }

        if (sendDisappearing)
        {
            await NavigationHelper.SendDisappearingAsync(page).ConfigureAwait(true);
        }

        var popCount = 1;
        await NavigationHelper.SendLeavingAsync(page).ConfigureAwait(true);

        var intermediatePages = actionDescriptor.IntermediatePages;
        if (intermediatePages is not null)
        {
            popCount += intermediatePages.Count;
            foreach (var intermediatePage in intermediatePages)
            {
                await NavigationHelper.SendLeavingAsync(intermediatePage).ConfigureAwait(true);
            }
        }

        await controller.PopAsync(popCount).ConfigureAwait(true);

        if (flags.HasFlag(NavigationActionFlags.SendAppearing))
        {
            await SendAppearingAndUpdateStackAsync(intent).ConfigureAwait(true);
        }

        return true;
    }

    private async Task<bool> ExecuteSetRootActionAsync(IShellNavigationController controller, NavigationActionDescriptor actionDescriptor)
    {
        var page = actionDescriptor.Page!;
        var pageModel = page.BindingContext!;
        var flags = actionDescriptor.Flags;
        var sendDisappearing = flags.HasFlag(NavigationActionFlags.SendDisappearing);

        if (flags.HasFlag(NavigationActionFlags.Guarded))
        {
            var canLeave = await ((ILeavingGuard)pageModel).CanLeaveAsync().ConfigureAwait(true);
            if (!canLeave)
            {
                if (!sendDisappearing)
                {
                    await SendAppearingAndUpdateStackAsync(null).ConfigureAwait(true);
                }

                return false;
            }
        }

        var pageModelType = actionDescriptor.Segment.PageModelType!;
        var segmentName = actionDescriptor.Segment.Segment;
        var targetPage = controller.GetRootPage(segmentName)
                         ?? throw new InvalidOperationException($"Unable to find shell content for {pageModelType.FullName}.");

        if (sendDisappearing)
        {
            await NavigationHelper.SendDisappearingAsync(page).ConfigureAwait(true);
        }

        await NavigationHelper.SendLeavingAsync(page).ConfigureAwait(true);

        var intent = actionDescriptor.Intent;

        await NavigationHelper.SendEnteringAsync(targetPage, intent).ConfigureAwait(true);

        await controller.SetRootPageAsync(segmentName).ConfigureAwait(true);

        if (flags.HasFlag(NavigationActionFlags.SendAppearing))
        {
            await SendAppearingAndUpdateStackAsync(intent).ConfigureAwait(true);
        }

        return true;
    }

    private async Task<bool> ExecutePushActionAsync(IShellNavigationController controller, NavigationActionDescriptor actionDescriptor)
    {
        var page = actionDescriptor.Page!;
        var flags = actionDescriptor.Flags;

        if (flags.HasFlag(NavigationActionFlags.SendDisappearing))
        {
            await NavigationHelper.SendDisappearingAsync(page).ConfigureAwait(true);
        }

        var targetPage = CreatePage(actionDescriptor.Segment.PageModelType!, controller.CurrentPage);
        var intent = actionDescriptor.Intent;

        await NavigationHelper.SendEnteringAsync(targetPage, intent).ConfigureAwait(true);

        await controller.PushAsync(targetPage).ConfigureAwait(true);

        if (flags.HasFlag(NavigationActionFlags.SendAppearing))
        {
            await SendAppearingAndUpdateStackAsync(intent).ConfigureAwait(true);
        }

        return true;
    }

    private ValueTask SendAppearingAndUpdateStackAsync(object? intent)
    {
        PageModelStack = NavigationHelper.EnumerateStackPageModels(Controller).ToList();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageModelStack)));
        return SendAppearingAsync(intent);
    }

    private ValueTask SendAppearingAsync(object? intent)
    {
        var page = Controller.CurrentPage;
        return NavigationHelper.SendAppearingAsync(page, intent);
    }

    private enum NavigationAction
    {
        Push,
        Pop,
        ReplaceRoot,
    }

    [Flags]
    private enum NavigationActionFlags
    {
        None = 0,
        SendAppearing = 1,
        SendDisappearing = 2,
        Guarded = 8,
    }

    private record NavigationActionDescriptor(
        NavigationAction Action,
        NavigationSegment Segment,
        NavigationActionFlags Flags,
        Page? Page,
        object? Intent,
        List<Page>? IntermediatePages = null);

    private static List<NavigationActionDescriptor> GetRelativeNavigationActions(IEnumerable<Page> navigationStack, RelativeNavigation navigation)
    {
        var stack = new Stack<(Page? Page, Type ModelType)>(navigationStack.Select(page => (Page: (Page?)page, ModelType: page.BindingContext.GetType())));
        List<NavigationActionDescriptor> actions = new(navigation.Count);
        NavigationActionDescriptor? previousAction = null;

        for (var i = 0; i < navigation.Count; i++)
        {
            var segment = navigation[i];
            var isFirstSegment = i == 0;
            var isLastSegment = i == navigation.Count - 1;
            var flags = NavigationActionFlags.None;
            var currentStackCount = stack.Count;
            object? intent = null;

            if (isFirstSegment)
            {
                flags |= NavigationActionFlags.SendDisappearing;
            }

            if (isLastSegment)
            {
                flags |= NavigationActionFlags.SendAppearing;
                intent = navigation.Intent;
            }

            if (segment.Segment == NavigationPop.PopSegment)
            {
                if (currentStackCount == 1 && isLastSegment)
                {
                    throw new InvalidOperationException("Cannot pop the root page.");
                }

                if (currentStackCount == 0)
                {
                    throw new InvalidOperationException("Cannot pop more pages than are currently on the navigation stack.");
                }

                var stackPage = stack.Pop();

                var targetPageModelType = stack.TryPeek(out var nextStackPage) ? nextStackPage.ModelType : null;
                NavigationHelper.AssertIntentAware(targetPageModelType, intent);

                if (stackPage.ModelType.IsAssignableTo(typeof(ILeavingGuard)))
                {
                    flags |= NavigationActionFlags.Guarded;
                }
                else if (currentStackCount > 1 && previousAction?.Action == NavigationAction.Pop && !previousAction.Flags.HasFlag(NavigationActionFlags.Guarded))
                {
                    var intermediatePages = previousAction.IntermediatePages ?? new List<Page>(3);
                    intermediatePages.Add(stackPage.Page!);
                    previousAction = actions[^1] = new NavigationActionDescriptor(NavigationAction.Pop, segment, previousAction.Flags | flags, previousAction.Page, intent, intermediatePages);
                    continue;
                }

                previousAction = new NavigationActionDescriptor(NavigationAction.Pop, segment, flags, stackPage.Page, intent);
                actions.Add(previousAction);
                continue;
            }

            NavigationHelper.AssertIntentAware(segment.PageModelType, intent);

            if (currentStackCount == 0 && previousAction!.Action == NavigationAction.Pop)
            {
                previousAction = actions[^1] = new NavigationActionDescriptor(NavigationAction.ReplaceRoot, segment, previousAction.Flags | flags, previousAction.Page, intent);
                stack.Push((null, segment.PageModelType!));
                continue;
            }

            previousAction = new NavigationActionDescriptor(NavigationAction.Push, segment, flags, stack.Peek().Page, intent);
            actions.Add(previousAction);
            stack.Push((null, segment.PageModelType!));
        }

        return actions;
    }
}
