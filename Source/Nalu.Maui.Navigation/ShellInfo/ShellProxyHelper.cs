using System.Collections.Specialized;

namespace Nalu;

internal class ShellProxyHelper
{
    public static void UpdateProxyItemsCollection<TItem, TProxy>(NotifyCollectionChangedEventArgs e, List<TProxy> items, Func<TItem, TProxy> itemFactory)
    {
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var insertAt = e.NewStartingIndex;

                    foreach (var item in e.NewItems!.Cast<TItem>())
                    {
                        items.Insert(insertAt++, itemFactory(item));
                    }

                    break;
                case NotifyCollectionChangedAction.Remove:
                    var removeAt = e.OldStartingIndex;
                    var removeCount = e.OldItems!.Count;

                    while (removeCount-- > 0)
                    {
                        var removedProxy = items[removeAt];
                        items.RemoveAt(removeAt);
                        (removedProxy as IDisposable)?.Dispose();
                    }

                    break;
                case NotifyCollectionChangedAction.Replace:
                    var replaceAt = e.OldStartingIndex;

                    foreach (var item in e.OldItems!.Cast<TItem>())
                    {
                        var replacedProxy = items[replaceAt];
                        items[replaceAt] = itemFactory(item);
                        (replacedProxy as IDisposable)?.Dispose();
                    }

                    break;
                case NotifyCollectionChangedAction.Move:
                    var moveAtOld = e.OldStartingIndex;
                    var moveAtNew = e.NewStartingIndex;
                    var movedProxy = items[moveAtOld];
                    items.RemoveAt(moveAtOld);
                    items.Insert(moveAtNew, movedProxy);

                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var item in items)
                    {
                        (item as IDisposable)?.Dispose();
                    }

                    items.Clear();

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
