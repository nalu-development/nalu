namespace Nalu;

/// <summary>Shared-element helpers used by the platform presenters (§8).</summary>
internal static class ScaffoldTransitions
{
    /// <summary>Collects all views with a <see cref="Scaffold.TransitionNameProperty"/> in a page's visual tree, keyed by name.</summary>
    public static Dictionary<string, View> Collect(Page page)
    {
        var result = new Dictionary<string, View>(StringComparer.Ordinal);

        void Walk(IVisualTreeElement element)
        {
            if (element is View view && Scaffold.GetTransitionName(view) is { Length: > 0 } name)
            {
                result[name] = view;
            }

            foreach (var child in element.GetVisualChildren())
            {
                Walk(child);
            }
        }

        Walk(page);

        return result;
    }

    /// <summary>The shared-element names present on BOTH pages (the pairs that will animate).</summary>
    public static IReadOnlyList<string> MatchingNames(Dictionary<string, View> from, Dictionary<string, View> to)
        => from.Keys.Where(to.ContainsKey).ToList();
}
