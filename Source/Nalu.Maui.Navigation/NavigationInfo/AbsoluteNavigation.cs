namespace Nalu;

#pragma warning disable SA1402
#pragma warning disable SA1649

/// <summary>
/// Represents the initial definition of an absolute navigation.
/// </summary>
public interface IAbsoluteNavigationInitialBuilder : INavigationInfo
{
    /// <summary>
    /// Navigates to <typeparamref name="TPage"/> shell content using the specified type.
    /// </summary>
    /// <typeparam name="TPage">The type of page used on the `ShellContent`.</typeparam>
    IAbsoluteNavigationBuilder ShellContent<TPage>()
        where TPage : class;

    /// <summary>
    /// Navigates to <typeparamref name="TPage"/> shell content marked with a custom route.
    /// </summary>
    /// <param name="customRoute">The custom route defined on `Route` property of `ShellContent`.</param>
    /// <typeparam name="TPage">The type of page used on the `ShellContent`.</typeparam>
    IAbsoluteNavigationBuilder ShellContent<TPage>(string customRoute)
        where TPage : class;
}

/// <summary>
/// Represents an absolute navigation.
/// </summary>
public interface IAbsoluteNavigationBuilder : INavigationInfo
{
    /// <summary>
    /// Adds a new page to the target navigation stack.
    /// </summary>
    /// <typeparam name="TPage">The page type to add.</typeparam>
    IAbsoluteNavigationBuilder Add<TPage>()
        where TPage : class;

    /// <summary>
    /// Sets the intent to be passed on the target page model.
    /// </summary>
    /// <param name="intent">The intent object.</param>
    INavigationInfo WithIntent(object? intent);
}

/// <summary>
/// Defines an absolute navigation.
/// </summary>
public partial class AbsoluteNavigation : Navigation, IAbsoluteNavigationBuilder, IAbsoluteNavigationInitialBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteNavigation"/> class.
    /// </summary>
    public AbsoluteNavigation()
        : base(true, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteNavigation"/> class with the specified behavior.
    /// </summary>
    /// <param name="behavior">Custom navigation behavior.</param>
    public AbsoluteNavigation(NavigationBehavior? behavior)
        : base(true, behavior)
    {
    }

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder ShellContent<TPage>()
        where TPage : class
    {
        if (Count != 0)
        {
            throw new InvalidOperationException("Cannot add a shell content on top of another one.");
        }

        Add(new NavigationSegment
        {
            Type = typeof(TPage),
        });

        return this;
    }

    /// <inheritdoc />
    public IAbsoluteNavigationBuilder ShellContent<TPage>(string customRoute)
        where TPage : class
    {
        if (Count != 0)
        {
            throw new InvalidOperationException("Cannot add a shell content on top of another one.");
        }

        Add(new NavigationSegment
        {
            Type = typeof(TPage),
            SegmentName = customRoute,
        });

        return this;
    }

    /// <inheritdoc />
    IAbsoluteNavigationBuilder IAbsoluteNavigationBuilder.Add<TPage>()
        where TPage : class
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Cannot add a page without adding a shell content first.");
        }

        Add(new NavigationSegment
        {
            Type = typeof(TPage),
        });

        return this;
    }

    /// <inheritdoc />
    INavigationInfo IAbsoluteNavigationBuilder.WithIntent(object? intent)
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Cannot set intent on an empty navigation.");
        }

        Intent = intent;
        return this;
    }
}
