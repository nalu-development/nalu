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
    public static Scaffold GetScaffold(this Element element)
        => element.GetScaffoldOrDefault()
           ?? throw new InvalidOperationException(
               $"{element.GetType().Name} is not hosted in a Scaffold. The scaffold is reachable only after the element has been parented (never from a constructor)."
           );

    /// <summary>
    /// Walks the logical parents up to the owning <see cref="Scaffold"/>, or null when the
    /// element is not (yet) hosted in one.
    /// </summary>
    public static Scaffold? GetScaffoldOrDefault(this Element element)
    {
        Element? current = element;

        while (current is not null and not Scaffold)
        {
            current = current.Parent;
        }

        return current as Scaffold;
    }
}
