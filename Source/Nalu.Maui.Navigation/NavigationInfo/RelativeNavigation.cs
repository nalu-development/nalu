namespace Nalu;

#pragma warning disable SA1402
#pragma warning disable SA1649

/// <summary>
/// A relative navigation where only push operations are allowed.
/// </summary>
public interface IRelativeNavigationInitialBuilder : INavigationInfo
{
    /// <summary>
    /// Pushes a new page onto the navigation stack.
    /// </summary>
    /// <typeparam name="TPage">The page type to be pushed.</typeparam>
    IRelativeNavigationPushOnlyBuilder Push<TPage>()
        where TPage : class;

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    IRelativeNavigationBuilder Pop();
}

/// <summary>
/// A relative navigation where only push operations are allowed.
/// </summary>
public interface IRelativeNavigationPushOnlyBuilder : INavigationInfo
{
    /// <summary>
    /// Pushes a new page onto the navigation stack.
    /// </summary>
    /// <typeparam name="TPage">The page type to be pushed.</typeparam>
    IRelativeNavigationPushOnlyBuilder Push<TPage>()
        where TPage : class;

    /// <summary>
    /// Sets the intent to be passed on the target page model.
    /// </summary>
    /// <param name="intent">The intent object.</param>
    INavigationInfo WithIntent(object? intent);
}

/// <summary>
/// A relative navigation.
/// </summary>
public interface IRelativeNavigationBuilder : INavigationInfo
{
    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    IRelativeNavigationBuilder Pop();

    /// <summary>
    /// Pushes a new page onto the navigation stack.
    /// </summary>
    /// <typeparam name="TPage">The page type to be pushed.</typeparam>
    IRelativeNavigationPushOnlyBuilder Push<TPage>()
        where TPage : class;

    /// <summary>
    /// Sets the intent to be passed on the target page model.
    /// </summary>
    /// <param name="intent">The intent object.</param>
    INavigationInfo WithIntent(object? intent);
}

/// <summary>
/// Defines a relative navigation.
/// </summary>
public class RelativeNavigation : Navigation, IRelativeNavigationInitialBuilder, IRelativeNavigationBuilder, IRelativeNavigationPushOnlyBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelativeNavigation"/> class.
    /// </summary>
    public RelativeNavigation()
        : base(false, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelativeNavigation"/> class with the specified behavior.
    /// </summary>
    /// <param name="behavior">The behavior to use during this relative navigation.</param>
    public RelativeNavigation(NavigationBehavior? behavior)
        : base(false, behavior)
    {
    }

    /// <inheritdoc cref="IRelativeNavigationBuilder.Pop" />
    public IRelativeNavigationBuilder Pop()
    {
        Add(new NavigationPop());
        return this;
    }

    /// <inheritdoc cref="IRelativeNavigationBuilder.Push{TPage}"/> />
    public IRelativeNavigationPushOnlyBuilder Push<TPage>()
        where TPage : class
    {
        Add(new NavigationSegment
        {
            Type = typeof(TPage),
        });
        return this;
    }

    /// <inheritdoc cref="IRelativeNavigationBuilder.WithIntent" />
    public INavigationInfo WithIntent(object? intent)
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Cannot set intent on an empty navigation.");
        }

        Intent = intent;
        return this;
    }
}
