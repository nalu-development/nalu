namespace Nalu;

using System.Collections;
using System.ComponentModel;

#pragma warning disable CA1067

/// <summary>
/// Represents a navigation request.
/// </summary>
public abstract class Navigation : BindableObject, IList<NavigationSegment>, IReadOnlyList<NavigationSegment>, IEquatable<Navigation>
{
    /// <inheritdoc cref="Intent"/>
    public static readonly BindableProperty IntentProperty = BindableProperty.Create(
        nameof(Intent),
        typeof(object),
        typeof(Navigation));

    /// <summary>
    /// Gets or sets the page model to be used for the current <see cref="ShellContent"/>.
    /// </summary>
    public static readonly BindableProperty PageModelProperty = BindableProperty.CreateAttached("PageModel", typeof(Type), typeof(Navigation), null, propertyChanged: PageModelPropertyChanged);

    /// <summary>
    /// Gets the page model to be used for the current <see cref="ShellContent"/>.
    /// </summary>
    /// <param name="bindable">The <see cref="ShellContent"/>.</param>
    [TypeConverter(typeof(TypeTypeConverter))]
    public static Type? GetPageModel(BindableObject bindable) => (Type?)bindable.GetValue(PageModelProperty);

    /// <summary>
    /// Sets the page model to be used for the current <see cref="ShellContent"/>.
    /// </summary>
    /// <param name="bindable">The <see cref="ShellContent"/>.</param>
    /// <param name="value">Type of the page model.</param>
    [TypeConverter(typeof(TypeTypeConverter))]
    public static void SetPageModel(BindableObject bindable, Type? value) => bindable.SetValue(PageModelProperty, value);

    /// <summary>
    /// Creates a new instance of <see cref="RelativeNavigation"/>.
    /// </summary>
    /// <param name="intent">Gets or sets an optional intent object to be received by target page model.</param>
    public static RelativeNavigation Relative(object? intent = null)
        => new() { Intent = intent };

    /// <summary>
    /// Creates a new instance of <see cref="AbsoluteNavigation"/>.
    /// </summary>
    /// <param name="intent">Gets or sets an optional intent object to be received by target page model.</param>
    public static AbsoluteNavigation Absolute(object? intent = null)
        => new() { Intent = intent };

    private readonly List<NavigationSegment> _list = new(4);

    /// <summary>
    /// Gets the path to navigate to.
    /// </summary>
    public virtual string Path => string.Join('/', _list);

    /// <summary>
    /// Gets or sets an optional intent object to be received by target page model.
    /// </summary>
    public object? Intent
    {
        get => GetValue(IntentProperty);
        set => SetValue(IntentProperty, value);
    }

    /// <inheritdoc cref="ICollection{T}.Count"/>
    public int Count => _list.Count;

    /// <inheritdoc cref="ICollection{T}.IsReadOnly"/>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the navigation action at the specific index.
    /// </summary>
    /// <param name="index">The index.</param>
    public NavigationSegment this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public IEnumerator<NavigationSegment> GetEnumerator() => _list.GetEnumerator();

    /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc cref="ICollection{T}.Add"/>
    public virtual void Add(NavigationSegment item)
    {
        if (item.Segment == NavigationPop.PopSegment && _list.Count > 0 &&
            _list[^1].Segment != NavigationPop.PopSegment)
        {
            throw new InvalidOperationException("Cannot add a pop segment after a non-pop segment.");
        }

        _list.Add(item);
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the <see cref="Navigation"/>.
    /// </summary>
    /// <param name="items">Navigation segments.</param>
    public void AddRange(IEnumerable<NavigationSegment> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <inheritdoc cref="ICollection{T}.Clear"/>
    public void Clear() => _list.Clear();

    /// <inheritdoc cref="ICollection{T}.Contains"/>
    public bool Contains(NavigationSegment item) => _list.Contains(item);

    /// <inheritdoc cref="ICollection{T}.CopyTo"/>
    public void CopyTo(NavigationSegment[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    /// <inheritdoc cref="ICollection{T}.Remove"/>
    public bool Remove(NavigationSegment item) => _list.Remove(item);

    /// <inheritdoc cref="IList{T}.IndexOf"/>
    public int IndexOf(NavigationSegment item) => _list.IndexOf(item);

    /// <inheritdoc cref="IList{T}.Insert"/>
    public virtual void Insert(int index, NavigationSegment item)
    {
        if (item.Segment == NavigationPop.PopSegment)
        {
            if (index > 0 && _list[index - 1].Segment != NavigationPop.PopSegment)
            {
                throw new InvalidOperationException("Cannot add a pop segment after a non-pop segment.");
            }
        }
        else
        {
            if (index < _list.Count && _list[index].Segment == NavigationPop.PopSegment)
            {
                throw new InvalidOperationException("Cannot add a segment before a pop segment.");
            }
        }

        _list.Insert(index, item);
    }

    /// <inheritdoc cref="IList{T}.RemoveAt"/>
    public void RemoveAt(int index) => _list.RemoveAt(index);

    /// <inheritdoc cref="IEquatable{Navigation}.Equals(Navigation)"/>
    public bool Equals(Navigation? other)
        => other is not null &&
           other.Path == Path &&
           other.Intent == Intent;

    /// <inheritdoc cref="object.Equals(object)"/>
    public override bool Equals(object? obj) => obj is Navigation navigation && Equals(navigation);

    /// <inheritdoc cref="object.GetHashCode"/>
    public override int GetHashCode()
    {
        unchecked
        {
            return Path.GetHashCode() + Intent?.GetHashCode() ?? 0;
        }
    }

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

        shellContent.Route = NavigationHelper.GetSegmentName(type);
        shellContent.Content = new ContentPage();
    }
}
