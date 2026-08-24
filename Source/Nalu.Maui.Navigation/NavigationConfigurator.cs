using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// Provides a fluent API for configuring Nalu navigation.
/// </summary>
public class NavigationConfigurator : INavigationConfiguration
{
    // ReSharper disable once InconsistentNaming
    private const DynamicallyAccessedMemberTypes DynamicallyAccessedPageModelMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;

    private readonly IServiceCollection _services;
    private readonly Dictionary<Type, Type> _mapping;
    private readonly HashSet<Type> _viewOnlyPages = [];
    private readonly HashSet<Type> _componentPages = [];

    /// <summary>
    /// Pages registered WITHOUT a page model via <see cref="AddPage{TPage}()"/>: they never
    /// enter <see cref="Mapping"/> (that dictionary is keyed by page-model type), but the
    /// snapshot restore still needs them to map persisted segment names back to page types
    /// across every registration style.
    /// </summary>
    internal IReadOnlyCollection<Type> ViewOnlyPages => _viewOnlyPages;

    /// <summary>
    /// Component types registered via <see cref="AddPage{TPage}()"/> (non-<see cref="Page"/>
    /// destinations rendered through an <see cref="IComponentPageFactory"/>). Like
    /// <see cref="ViewOnlyPages"/> they never enter <see cref="Mapping"/>: the component type
    /// IS the navigation segment identity.
    /// </summary>
    internal IReadOnlySet<Type> ComponentPages => _componentPages;

    /// <inheritdoc />
    public ImageSource? MenuImage { get; private set; }

    /// <inheritdoc />
    public ImageSource? BackImage { get; private set; }

    /// <inheritdoc />
    public NavigationIntentBehavior NavigationIntentBehavior { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, Type> Mapping => _mapping;

    /// <inheritdoc />
    public NavigationLeakDetectorState LeakDetectorState { get; private set; } = NavigationLeakDetectorState.EnabledWithDebugger;

    internal NavigationConfigurator(IServiceCollection services)
    {
        _mapping = [];
        _services = services.AddSingleton<INavigationConfiguration>(this);
    }

    /// <summary>
    /// Sets the navigation leak detector state.
    /// </summary>
    /// <param name="state">Whether the leak detector should be enabled or not.</param>
    public NavigationConfigurator WithLeakDetectorState(NavigationLeakDetectorState state)
    {
        LeakDetectorState = state;

        return this;
    }

    /// <summary>
    /// Sets back navigation image.
    /// </summary>
    /// <param name="imageSource">Image to use for back navigation button.</param>
    public NavigationConfigurator WithBackImage(ImageSource imageSource)
    {
        BackImage = imageSource;

        return this;
    }

    /// <summary>
    /// Sets back navigation image.
    /// </summary>
    /// <param name="imageSource">Image to use for the back navigation button.</param>
    public NavigationConfigurator WithMenuImage(ImageSource imageSource)
    {
        MenuImage = imageSource;

        return this;
    }

    /// <summary>
    /// Defines how lifecycle events should be handled when intent is detected.
    /// </summary>
    /// <param name="behavior">The behavior to use.</param>
    public NavigationConfigurator WithNavigationIntentBehavior(NavigationIntentBehavior behavior)
    {
        NavigationIntentBehavior = behavior;

        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TPage" /> as a directly navigable destination WITHOUT a
    /// page model: navigate to it with <c>Navigation.Relative().Push&lt;TPage&gt;()</c>
    /// (or use it as a root page type). Two flavors share this registration style:
    /// <list type="bullet">
    /// <item>
    /// <b>View-only page</b> (<typeparamref name="TPage" /> derives from <see cref="Page" />):
    /// the page itself is the navigation lifecycle target — implement
    /// <see cref="IEnteringAware" />, <see cref="ILeavingGuard" />, intent interfaces…
    /// directly on the page. Assigning a <c>BindingContext</c> explicitly hands the lifecycle
    /// over to it entirely (the standard MVVM contract); an inherited binding context does not.
    /// </item>
    /// <item>
    /// <b>Component page</b> (any other class, e.g. a MauiReactor <c>Component</c>): the native
    /// page is produced at navigation time by the registered <see cref="IComponentPageFactory" />
    /// (provided by an adapter package), and the component is the navigation lifecycle target —
    /// implement the lifecycle interfaces directly on the component.
    /// </item>
    /// </list>
    /// Adds <typeparamref name="TPage" /> as a scoped service (a fresh instance per navigation,
    /// created in the page's own scope — constructor-inject services freely).
    /// </summary>
    /// <typeparam name="TPage">Type of the page or component.</typeparam>
    public NavigationConfigurator AddPage<[DynamicallyAccessedMembers(DynamicallyAccessedPageModelMembers)] TPage>()
        where TPage : class
    {
        var registry = typeof(Page).IsAssignableFrom(typeof(TPage)) ? _viewOnlyPages : _componentPages;

        if (registry.Add(typeof(TPage)))
        {
            _services.AddScoped<TPage>();
        }

        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TPage" /> as the view for <typeparamref name="TPageModel" />.
    /// Adds <typeparamref name="TPage" /> and <typeparamref name="TPageModel" /> as scoped services.
    /// </summary>
    /// <typeparam name="TPageModel">Type of the page model.</typeparam>
    /// <typeparam name="TPage">Type of the page.</typeparam>
    public NavigationConfigurator AddPage<[DynamicallyAccessedMembers(DynamicallyAccessedPageModelMembers)] TPageModel, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>()
        where TPage : ContentPage
        where TPageModel : class, INotifyPropertyChanged
        => AddPage(typeof(TPageModel), typeof(TPage));
    
    /// <summary>
    /// Registers <typeparamref name="TPage" /> as the view for <typeparamref name="TPageModel" />.
    /// Adds <typeparamref name="TPage" /> and <typeparamref name="TPageModel" /> as scoped services.
    /// </summary>
    /// <typeparam name="TPageModel">Type of the page model.</typeparam>
    /// <typeparam name="TPageModelImplementation">Type of the page model implementation.</typeparam>
    /// <typeparam name="TPage">Type of the page.</typeparam>
    public NavigationConfigurator AddPage<TPageModel, [DynamicallyAccessedMembers(DynamicallyAccessedPageModelMembers)] TPageModelImplementation, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>()
        where TPage : ContentPage
        where TPageModel : class, INotifyPropertyChanged
        where TPageModelImplementation : TPageModel
        => AddPage(typeof(TPageModel), typeof(TPageModelImplementation), typeof(TPage));

    /// <summary>
    /// Registers <paramref name="pageType" /> as the view for <paramref name="pageModelType" />.
    /// Adds <paramref name="pageType" /> and <paramref name="pageModelType" /> as scoped services.
    /// </summary>
    /// <param name="pageModelType">Type of the page model.</param>
    /// <param name="pageType">Type of the page.</param>
    public NavigationConfigurator AddPage(
        [DynamicallyAccessedMembers(DynamicallyAccessedPageModelMembers)] Type pageModelType, 
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pageType)
    {
        if (_mapping.TryAdd(pageModelType, pageType))
        {
            _services
                .AddScoped(pageModelType)
                .AddScoped(pageType);
        }

        return this;
    }

    /// <summary>
    /// Registers <paramref name="pageType" /> as the view for <paramref name="pageModelType" />.
    /// Adds <paramref name="pageType" /> and <paramref name="pageModelType" /> as scoped services.
    /// </summary>
    /// <param name="pageModelType">Type of the page model interface.</param>
    /// <param name="pageModelImplementationType">Type of the page model implementation.</param>
    /// <param name="pageType">Type of the page.</param>
    public NavigationConfigurator AddPage(
        Type pageModelType,
        [DynamicallyAccessedMembers(DynamicallyAccessedPageModelMembers)] Type pageModelImplementationType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pageType
    )
    {
        if (_mapping.TryAdd(pageModelType, pageType))
        {
            _services
                .AddScoped(pageModelType, pageModelImplementationType)
                .AddScoped(pageType);
        }

        return this;
    }

}
