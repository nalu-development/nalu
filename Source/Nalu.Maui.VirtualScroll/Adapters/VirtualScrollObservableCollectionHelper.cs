using System.Collections.ObjectModel;
using System.Reflection;

namespace Nalu;

internal static class VirtualScrollObservableCollectionHelper
{
    public static Type? GetObservableCollectionItemType(Type collectionType)
    {
        // Loop through type and base types to find `ObservableCollection<T>` and return `T`
        var currentType = collectionType;

        while (currentType != null)
        {
            if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                return currentType.GetGenericArguments()[0];
            }

            currentType = currentType.BaseType;
        }

        return null;
    }
    
    public static MethodInfo CreateMoveInObservableCollectionMethodInfo(Type itemType)
        => typeof(VirtualScrollObservableCollectionHelper)
            .GetMethod(nameof(MoveInObservableCollection), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(itemType);
    
    private static void MoveInObservableCollection<T>(ObservableCollection<T> source, ObservableCollection<T> destination, VirtualScrollDragMoveInfo info)
    {
        if (source == destination)
        {
            source.Move(info.CurrentItemIndex, info.DestinationItemIndex);
        }
        else
        {
            source.RemoveAt(info.CurrentItemIndex);
            destination.Insert(info.DestinationItemIndex, (T)info.Item!);
        }
    }
}
