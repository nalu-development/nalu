using Foundation;
using Microsoft.Maui.Controls.Internals;
using UIKit;

namespace Nalu;

internal sealed class VirtualScrollPlatformReuseIdManager
{
    private readonly HashSet<string> _registeredIds = [];
    private readonly UICollectionView _collectionView;

    public string DefaultReuseId => "DEFAULT";

    public VirtualScrollPlatformReuseIdManager(UICollectionView collectionView)
    {
        _collectionView = collectionView;
        _registeredIds.Add(DefaultReuseId);
        _collectionView.RegisterClassForCell(typeof(UICollectionViewCell), "DEFAULT");
    }

    public string GetReuseId(DataTemplate template, string? supplementaryKind = null) => ItemReuseId(template, supplementaryKind ?? "ITEM", supplementaryKind);

    private string ItemReuseId(DataTemplate template, string prefix, string? supplementaryKind = null)
    {
        IDataTemplateController dataTemplateController = template;
        var templateId = dataTemplateController.IdString;
        var reuseId = $"{prefix}_{templateId}";
        if (_registeredIds.Add(reuseId))
        {
            if (supplementaryKind is null) {
                _collectionView.RegisterClassForCell(typeof(VirtualScrollCell), reuseId);
            }
            else
            {
                _collectionView.RegisterClassForSupplementaryView(typeof(VirtualScrollCell), new NSString(supplementaryKind), reuseId);
            }
        }

        return reuseId;
    }
}
