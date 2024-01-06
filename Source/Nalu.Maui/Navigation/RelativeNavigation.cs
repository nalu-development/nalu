namespace Nalu;

using System.ComponentModel;

/// <summary>
/// Defines a relative navigation.
/// </summary>
public class RelativeNavigation : Navigation
{
    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    public RelativeNavigation Pop()
    {
        Add(new NavigationPop());
        return this;
    }

    /// <summary>
    /// Pushes a new page registered as <typeparamref name="TPageModel"/> onto the navigation stack.
    /// </summary>
    /// <typeparam name="TPageModel">The page model to push.</typeparam>
    public RelativeNavigation Push<TPageModel>()
        where TPageModel : class, INotifyPropertyChanged
    {
        Add(typeof(TPageModel));
        return this;
    }
}
