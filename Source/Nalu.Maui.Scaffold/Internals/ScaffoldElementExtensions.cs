namespace Nalu;

/// <summary>Element-tree helpers shared by scaffold chrome elements.</summary>
internal static class ScaffoldElementExtensions
{
    /// <summary>Walks the logical parents up to the owning <see cref="Scaffold"/>, or null when detached.</summary>
    public static Scaffold? FindScaffold(this Element element)
    {
        Element? current = element;

        while (current is not null and not Scaffold)
        {
            current = current.Parent;
        }

        return current as Scaffold;
    }
}
