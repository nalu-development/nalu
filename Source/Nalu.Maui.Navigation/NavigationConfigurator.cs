namespace Nalu;

using System.ComponentModel;
using System.Reflection;

/// <summary>
/// Provides a fluent API for configuring Nalu navigation.
/// </summary>
public class NavigationConfigurator : INavigationConfiguration
{
    private readonly IServiceCollection _services;
    private readonly Type _applicationType;
    private readonly Dictionary<Type, Type> _mapping;

    /// <inheritdoc />
    public ImageSource MenuImage { get; private set; }

    /// <inheritdoc />
    public ImageSource BackImage { get; private set; } = null!;

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, Type> Mapping => _mapping;

    /// <inheritdoc />
    public NavigationLeakDetectorState LeakDetectorState { get; private set; } = NavigationLeakDetectorState.EnabledWithDebugger;

    internal NavigationConfigurator(IServiceCollection services, Type applicationType)
    {
        _mapping = [];
        _applicationType = applicationType;
        _services = services.AddSingleton<INavigationConfiguration>(this);
        MenuImage = ImageSource.FromFile("nalu_navigation_menu.png");

        var isApple = OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();
        _ = isApple ? WithAppleBackImage() : WithAndroidBackImage();
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
    /// <param name="imageSource">Image to use for back navigation button.</param>
    public NavigationConfigurator WithMenuImage(ImageSource imageSource)
    {
        MenuImage = imageSource;
        return this;
    }

    /// <summary>
    /// Uses android-style back navigation image.
    /// </summary>
    public NavigationConfigurator WithAndroidBackImage()
    {
        BackImage = ImageSource.FromFile("nalu_navigation_arrow_back_android.png");
        return this;
    }

    /// <summary>
    /// Uses ios-style back navigation image.
    /// </summary>
    public NavigationConfigurator WithAppleBackImage()
    {
        BackImage = ImageSource.FromFile("nalu_navigation_arrow_back_ios.png");
        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TPage"/> as the view for <typeparamref name="TPageModel"/>.
    /// Adds <typeparamref name="TPage"/> and <typeparamref name="TPageModel"/> as scoped services.
    /// </summary>
    /// <typeparam name="TPageModel">Type of the page model.</typeparam>
    /// <typeparam name="TPage">Type of the page.</typeparam>
    public NavigationConfigurator AddPage<TPageModel, TPage>()
        where TPage : ContentPage
        where TPageModel : class, INotifyPropertyChanged
        => AddPage(typeof(TPageModel), typeof(TPage));

    /// <summary>
    /// Registers <typeparamref name="TPage"/> as the view for <typeparamref name="TPageModel"/>.
    /// Adds <typeparamref name="TPage"/> and <typeparamref name="TPageModel"/> as scoped services.
    /// </summary>
    /// <typeparam name="TPageModel">Type of the page model.</typeparam>
    /// <typeparam name="TPageModelImplementation">Type of the page model implementation.</typeparam>
    /// <typeparam name="TPage">Type of the page.</typeparam>
    public NavigationConfigurator AddPage<TPageModel, TPageModelImplementation, TPage>()
        where TPage : ContentPage
        where TPageModel : class, INotifyPropertyChanged
        where TPageModelImplementation : TPageModel
        => AddPage(typeof(TPageModel), typeof(TPageModelImplementation), typeof(TPage));

    /// <summary>
    /// Registers <paramref name="pageType"/> as the view for <paramref name="pageModelType"/>.
    /// Adds <paramref name="pageType"/> and <paramref name="pageModelType"/> as scoped services.
    /// </summary>
    /// <param name="pageModelType">Type of the page model.</param>
    /// <param name="pageType">Type of the page.</param>
    public NavigationConfigurator AddPage(Type pageModelType, Type pageType)
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
    /// Registers <paramref name="pageType"/> as the view for <paramref name="pageModelType"/>.
    /// Adds <paramref name="pageType"/> and <paramref name="pageModelType"/> as scoped services.
    /// </summary>
    /// <param name="pageModelType">Type of the page model interface.</param>
    /// <param name="pageModelImplementationType">Type of the page model implementation.</param>
    /// <param name="pageType">Type of the page.</param>
    public NavigationConfigurator AddPage(Type pageModelType, Type pageModelImplementationType, Type pageType)
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
    /// Registers all <see cref="ContentPage"/>s matching a page model via default naming convention
    /// `MyPage => MyPageModel` naming convention and adds them all as scoped services.
    /// </summary>
    /// <param name="otherAssemblies">Assemblies to look for pages and page models.</param>
    public NavigationConfigurator AddPages(params Assembly[] otherAssemblies)
        => AddPages(pageName => $"{pageName}Model", otherAssemblies);

    /// <summary>
    /// Registers all <see cref="ContentPage"/>s matching a page model via provided
    /// `<paramref name="pageToModelNameConvention"/>` naming convention and adds them all as scoped services.
    /// </summary>
    /// <remarks>If corresponding interface is found `IMyPageModel` the view model will be registered through the interface.</remarks>
    /// <param name="pageToModelNameConvention">Given a page class name returns the corresponding page model class name.</param>
    /// <param name="otherAssemblies">Assemblies to look for pages and page models.</param>
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
