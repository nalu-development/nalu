namespace Nalu;

using System.Collections;
using System.ComponentModel;
using Nalu.Internals;

#pragma warning disable CA1067
#pragma warning disable IDE0290

/// <summary>
/// Represents a navigation request.
/// </summary>
public abstract class Navigation : BindableObject, IList<INavigationSegment>, INavigationInfo
{
    /// <summary>
    /// Gets or sets the page model to be used for the current <see cref="ShellContent"/>.
    /// </summary>
    public static readonly BindableProperty PageTypeProperty = BindableProperty.CreateAttached("PageType", typeof(Type), typeof(Navigation), null, propertyChanged: PageModelPropertyChanged);

    /// <summary>
    /// Gets the page model to be used for the current <see cref="ShellContent"/>.
    /// </summary>
    /// <param name="bindable">The <see cref="ShellContent"/>.</param>
    [TypeConverter(typeof(TypeTypeConverter))]
    public static Type? GetPageType(BindableObject bindable) => (Type?)bindable.GetValue(PageTypeProperty);

    /// <summary>
    /// Sets the page model to be used for the current <see cref="ShellContent"/>.
    /// </summary>
    /// <param name="bindable">The <see cref="ShellContent"/>.</param>
    /// <param name="value">Type of the page model.</param>
    [TypeConverter(typeof(TypeTypeConverter))]
    public static void SetPageType(BindableObject bindable, Type? value)
    {
        if (value?.GetType().IsSubclassOf(typeof(Page)) != true)
        {
            throw new InvalidOperationException("PageType must be a type that inherits from Page.");
        }

        bindable.SetValue(PageTypeProperty, value);
    }

    /// <summary>
    /// Defines the intent property.
    /// </summary>
    public static readonly BindableProperty IntentProperty = BindableProperty.Create(
        nameof(Intent),
        typeof(object),
        typeof(NavigationSegment));

    /// <summary>
    /// Creates a fluent <see cref="RelativeNavigation"/> builder.
    /// </summary>
    /// <param name="behavior">Applies a specific behavior to this navigation instead of using the default one.</param>
    public static IRelativeNavigationInitialBuilder Relative(NavigationBehavior? behavior = null)
        => new RelativeNavigation(behavior);

    /// <summary>
    /// Creates a fluent <see cref="AbsoluteNavigation"/> builder.
    /// </summary>
    /// <param name="behavior">Applies a specific behavior to this navigation instead of using the default one.</param>
    public static IAbsoluteNavigationInitialBuilder Absolute(NavigationBehavior? behavior = null)
        => new AbsoluteNavigation(behavior);

    private readonly List<INavigationSegment> _list = new(4);

    /// <inheritdoc />
    public NavigationBehavior? Behavior { get; }

    /// <inheritdoc />
    public bool IsAbsolute { get; }

    /// <inheritdoc />
    public object? Intent
    {
        get => GetValue(IntentProperty);
        set => SetValue(IntentProperty, value);
    }

    /// <inheritdoc />
    public string Path => IsAbsolute ? "//" + string.Join('/', _list) : string.Join('/', _list);

    /// <inheritdoc cref="ICollection{T}.Count"/>
    public int Count => _list.Count;

    /// <inheritdoc cref="ICollection{T}.IsReadOnly"/>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the navigation action at the specific index.
    /// </summary>
    /// <param name="index">The index.</param>
    public INavigationSegment this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Navigation"/> class.
    /// </summary>
    /// <param name="isAbsolute">Tells whether this is an absolute navigation.</param>
    /// <param name="behavior">Specifies a custom navigation behavior.</param>
    protected Navigation(bool isAbsolute, NavigationBehavior? behavior)
    {
        IsAbsolute = isAbsolute;
        Behavior = behavior;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public IEnumerator<INavigationSegment> GetEnumerator() => _list.GetEnumerator();

    /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    /// <inheritdoc cref="ICollection{T}.Add"/>
    public void Add(INavigationSegment item)
    {
        if (IsAbsolute && item.SegmentName == NavigationPop.PopRoute)
        {
            throw new InvalidOperationException("Cannot add a pop segment to an absolute navigation.");
        }

        if (item.SegmentName == NavigationPop.PopRoute && _list.Count > 0 &&
            _list[^1].SegmentName != NavigationPop.PopRoute)
        {
            throw new InvalidOperationException("Cannot add a pop segment after a non-pop segment.");
        }

        _list.Add(item);
    }

    /// <inheritdoc cref="ICollection{T}.Clear"/>
    public void Clear() => _list.Clear();

    /// <inheritdoc cref="ICollection{T}.Contains"/>
    public bool Contains(INavigationSegment item) => _list.Contains(item);

    /// <inheritdoc cref="ICollection{T}.CopyTo"/>
    public void CopyTo(INavigationSegment[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    /// <inheritdoc cref="ICollection{T}.Remove"/>
    public bool Remove(INavigationSegment item) => _list.Remove(item);

    /// <inheritdoc cref="IList{T}.IndexOf"/>
    public int IndexOf(INavigationSegment item) => _list.IndexOf(item);

    /// <inheritdoc cref="IList{T}.Insert"/>
    public void Insert(int index, INavigationSegment item)
    {
        if (IsAbsolute && item.SegmentName == NavigationPop.PopRoute)
        {
            throw new InvalidOperationException("Cannot add a pop segment to an absolute navigation.");
        }

        if (item.SegmentName == NavigationPop.PopRoute)
        {
            if (index > 0 && _list[index - 1].SegmentName != NavigationPop.PopRoute)
            {
                throw new InvalidOperationException("Cannot add a pop segment after a non-pop segment.");
            }
        }
        else
        {
            if (index < _list.Count && _list[index].SegmentName == NavigationPop.PopRoute)
            {
                throw new InvalidOperationException("Cannot add a segment before a pop segment.");
            }
        }

        _list.Insert(index, item);
    }

    /// <inheritdoc cref="IList{T}.RemoveAt"/>
    public void RemoveAt(int index) => _list.RemoveAt(index);

    private static void PageModelPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is not ShellContent shellContent)
        {
            throw new InvalidOperationException("ShellPage can only be attached to ShellContent");
        }

        if (newvalue is not Type type || !typeof(INotifyPropertyChanged).IsAssignableFrom(type))
        {
            throw new InvalidOperationException("ShellPage must be a type that implements INotifyPropertyChanged");
        }

        if (shellContent.Route.StartsWith("D_FAULT_", StringComparison.Ordinal))
        {
            shellContent.Route = NavigationSegmentAttribute.GetSegmentName(type);
        }

#pragma warning disable IDE0053
        shellContent.ContentTemplate = new DataTemplate(() =>
#pragma warning restore IDE0053
        {
            var shell = (Shell?)shellContent.Parent?.Parent?.Parent;
            var serviceProvider = shell?.Handler?.GetServiceProvider() ?? throw new InvalidOperationException("Cannot provide shell content while detached from active Shell.");
            var navigationService = (NavigationService)(serviceProvider.GetService<INavigationService>() ?? throw new InvalidOperationException("MauiAppBuilder must be configured with UseNaluNavigation()."));
            var navigationConfiguration = serviceProvider.GetService<INavigationConfiguration>() ?? throw new InvalidOperationException("MauiAppBuilder must be configured with UseNaluNavigation().");
            var pageType = NavigationHelper.GetPageType(type, navigationConfiguration);
            var page = navigationService.CreatePage(pageType, null);
            ConfigureRootPage(page, shell, navigationConfiguration);
            return page;
        });
    }

#pragma warning disable IDE0051
    private static void ConfigureRootPage(Page page, Shell shell, INavigationConfiguration navigationConfiguration)
#pragma warning restore IDE0051
    {
        var backButtonBehavior = Shell.GetBackButtonBehavior(page);

#if ANDROID
        // https://github.com/dotnet/maui/issues/7045
        backButtonBehavior.Command = null;
#else
        backButtonBehavior.Command = new Command(() => _ = shell.FlyoutIsPresented = true);
#endif
        var color = Shell.GetTitleColor(page.IsSet(Shell.TitleColorProperty) ? page : shell);
        backButtonBehavior.IconOverride = NavigationService.WithColor(navigationConfiguration.MenuImage, color);
    }
}
