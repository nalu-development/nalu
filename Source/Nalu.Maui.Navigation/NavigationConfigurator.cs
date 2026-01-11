using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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
    private readonly Type _applicationType;
    private readonly Dictionary<Type, Type> _mapping;

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

    internal NavigationConfigurator(IServiceCollection services, Type applicationType)
    {
        _mapping = [];
        _applicationType = applicationType;
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

    /// <summary>
    /// Registers all <see cref="ContentPage" />s matching a page model via default naming convention
    /// `MyPage => MyPageModel` naming convention and adds them all as scoped services.
    /// </summary>
    /// <param name="otherAssemblies">Assemblies to look for pages and page models.</param>
    [RequiresUnreferencedCode("This method uses reflection to scan types in assemblies, which is not trim-compatible. Use AddPage method for each page instead.")]
    public NavigationConfigurator AddPages(params Assembly[] otherAssemblies)
        => AddPages(pageName => $"{pageName}Model", otherAssemblies);

    /// <summary>
    /// Registers all <see cref="ContentPage" />s matching a page model via provided
    /// `<paramref name="pageToModelNameConvention" />` naming convention and adds them all as scoped services.
    /// </summary>
    /// <remarks>If a corresponding interface is found `IMyPageModel` the view model will be registered through the interface.</remarks>
    /// <param name="pageToModelNameConvention">Given a page class name returns the corresponding page model class name.</param>
    /// <param name="otherAssemblies">Assemblies to look for pages and page models.</param>
    [RequiresUnreferencedCode("This method uses reflection to scan types in assemblies, which is not trim-compatible. Use AddPage method for each page instead.")]
    public NavigationConfigurator AddPages(Func<string, string> pageToModelNameConvention, params Assembly[] otherAssemblies)
    {
        var assemblies = new[] { _applicationType.Assembly }.Concat(otherAssemblies).Distinct();
        var types = assemblies.SelectMany(a => a.GetTypes()).ToList();

        var notifyPropertyChangedInterfaces = types
                                              .Where(t => t.IsInterface && t.IsAssignableTo(typeof(INotifyPropertyChanged)))
                                              .GroupBy(t => t.Name)
                                              .ToDictionary(g => g.Key, g => g.First());

        var notifyPropertyChangedClasses = types
                                           .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(INotifyPropertyChanged)))
                                           .GroupBy(t => t.Name)
                                           .ToDictionary(g => g.Key, g => g.First());

        var pageTypes = types.Where(t => t.IsSubclassOf(typeof(ContentPage)));

        foreach (var pageType in pageTypes)
        {
            var pageModelTypeName = pageToModelNameConvention(pageType.Name);

            if (!notifyPropertyChangedClasses.TryGetValue(pageModelTypeName, out var pageModelType))
            {
                continue;
            }

            var pageModelInterfaceTypeName = $"I{pageModelTypeName}";

            if (notifyPropertyChangedInterfaces.TryGetValue(pageModelInterfaceTypeName, out var pageModelInterfaceType) &&
                _mapping.TryAdd(pageModelInterfaceType, pageType))
            {
                _services
                    .AddScoped(pageModelInterfaceType, pageModelType)
                    .AddScoped(pageType);
            }
            else if (_mapping.TryAdd(pageModelType, pageType))
            {
                _services
                    .AddScoped(pageModelType)
                    .AddScoped(pageType);
            }
        }

        return this;
    }
}
