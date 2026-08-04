namespace Nalu;

/// <summary>Element-tree helpers for reaching the owning <see cref="Scaffold"/>.</summary>
public static class ScaffoldElementExtensions
{
    /// <summary>
    /// Walks the logical parents up to the owning <see cref="Scaffold"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The element is not hosted in a <see cref="Scaffold"/>. Note that the scaffold is only
    /// reachable AFTER the element has been parented — calling this from a constructor always
    /// throws; use it from event handlers, commands, or lifecycle callbacks instead (or use
    /// <see cref="GetScaffoldOrDefault"/> to probe).
    /// </exception>
    public static Scaffold GetScaffold(this IElement element)
        => element.GetScaffoldOrDefault()
           ?? throw new InvalidOperationException(
               $"{element.GetType().Name} is not hosted in a Scaffold. The scaffold is reachable only after the element has been parented (never from a constructor)."
           );

    /// <summary>
    /// Walks the logical parents up to the owning <see cref="Scaffold"/>, or null when none is
    /// reachable. When the walk reaches the application (including calling this ON the
    /// application itself — <see cref="Application"/> is an <see cref="IElement"/>), the first
    /// scaffold-hosted window's scaffold is returned instead: the app-level lookup and the
    /// element-level walk are ONE pair of methods on purpose (no conflicting overloads).
    /// </summary>
    public static Scaffold? GetScaffoldOrDefault(this IElement element)
    {
        IElement? current = element;

        while (current is not null)
        {
            switch (current)
            {
                case Scaffold scaffold:
                    return scaffold;

                case IApplication application:
                    return FirstWindowScaffold(application);
            }

            current = current.Parent;
        }

        return null;

        static Scaffold? FirstWindowScaffold(IApplication application)
        {
            foreach (var window in application.Windows)
            {
                if (window.Content is Scaffold scaffold)
                {
                    return scaffold;
                }
            }

            return null;
        }
    }
}
