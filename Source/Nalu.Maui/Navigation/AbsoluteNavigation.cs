namespace Nalu;

using System.ComponentModel;

/// <summary>
/// Defines an absolute navigation.
/// </summary>
public class AbsoluteNavigation : Navigation
{
    /// <inheritdoc cref="Navigation.Path"/>
    public override string Path => $"//{base.Path}";

    /// <summary>
    /// Pushes a new page registered as <typeparamref name="TPageModel"/> onto the navigation stack.
    /// </summary>
    /// <typeparam name="TPageModel">The page model to push.</typeparam>
    public AbsoluteNavigation Add<TPageModel>()
        where TPageModel : class, INotifyPropertyChanged
    {
        Add(typeof(TPageModel));
        return this;
    }

    /// <inheritdoc />
    public override void Insert(int index, NavigationSegment item)
    {
        if (item.Segment == NavigationPop.PopSegment)
        {
            throw new InvalidOperationException("Cannot insert a pop segment to an absolute navigation.");
        }

        base.Insert(index, item);
    }

    /// <inheritdoc />
    public override void Add(NavigationSegment item)
    {
        if (item.Segment == NavigationPop.PopSegment)
        {
            throw new InvalidOperationException("Cannot add a pop segment to an absolute navigation.");
        }

        base.Add(item);
    }
}
