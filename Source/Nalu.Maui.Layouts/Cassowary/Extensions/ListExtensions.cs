namespace Nalu.Cassowary.Extensions;

internal static class ListExtensions
{
    public static T? Pop<T>(this List<T> list)
    {
        if (list.Count == 0)
        {
            return default;
        }

        var last = list[^1];
        list.RemoveAt(list.Count - 1);
        return last;
    }
}
