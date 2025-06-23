namespace Nalu;

internal static class AppIds
{
    private static readonly Dictionary<string, int> _idCache = [];
    
    public static int GetId(string fieldName, Android.Views.View view)
    {
        if (_idCache.TryGetValue(fieldName, out var cachedId))
        {
            return cachedId;
        }

        var context = view.Context!;
        var id = context.Resources!.GetIdentifier(fieldName, "id", context.PackageName);

        if (id == 0)
        {
            Android.Util.Log.Error("SoftKeyboardManager", "Failed to find values.xml identifier for field: " + fieldName);

            return id;
        }

        _idCache[fieldName] = id;

        return id;
    }
}
