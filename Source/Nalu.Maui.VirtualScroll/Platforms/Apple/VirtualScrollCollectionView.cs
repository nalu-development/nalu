using System.Reflection;
using System.Runtime.CompilerServices;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;
using ViewExtensions = Microsoft.Maui.Platform.ViewExtensions;

namespace Nalu;

internal interface IVirtualScrollCellsLayoutController
{
    bool NeedsCellsLayout { get; }
    void SetCellLayoutCompleted();
}

/// <summary>
/// Patched UICollectionView for <see cref="VirtualScroll"/> support.
/// </summary>
public class VirtualScrollCollectionView : UICollectionView, IVirtualScrollCellsLayoutController
#if NET10_0_OR_GREATER
    , Microsoft.Maui.Platform.IPlatformMeasureInvalidationController
#endif
{
    private readonly IVirtualScrollLayoutInfo _layoutInfo;
    private readonly VirtualScrollCellManager<VirtualScrollCell> _cellManager = new(cell => cell.VirtualView);
    private readonly List<NSIndexPath> _invalidatedGlobalHeaders = new(16);
    private readonly List<NSIndexPath> _invalidatedGlobalFooters = new(16);
    private readonly List<NSIndexPath> _invalidatedSectionHeaders = new(16);
    private readonly List<NSIndexPath> _invalidatedSectionFooters = new(16);
    private readonly List<NSIndexPath> _invalidatedPaths = new(16);
    private bool _needsCellsLayout;
    private CGSize _lastContentSize;

    bool IVirtualScrollCellsLayoutController.NeedsCellsLayout => _needsCellsLayout;

    /// <summary>
    /// Event raised when the content size changes.
    /// </summary>
    internal event EventHandler<EventArgs>? ContentSizeChanged;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollCollectionView" /> class.
    /// </summary>
    public VirtualScrollCollectionView(CGRect frame, UICollectionViewLayout layout, IVirtualScrollLayoutInfo layoutInfo) : base(frame, layout)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        AutomaticallyAdjustsScrollIndicatorInsets = true;
        _layoutInfo = layoutInfo;
        _lastContentSize = frame.Size;
    }

    internal void SetNeedsCellsLayout() => _needsCellsLayout = true;

    /// <inheritdoc/>
    public override void LayoutSubviews()
    {
        InvalidateNeedingMeasureCells();
        base.LayoutSubviews();
        
        // Detect content size changes to update fading edge
        var contentSize = ContentSize;
        if (!contentSize.Equals(_lastContentSize))
        {
            _lastContentSize = contentSize;
            ContentSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    private void InvalidateNeedingMeasureCells()
    {
        var cellsLayoutController = (IVirtualScrollCellsLayoutController) this;

        if (!cellsLayoutController.NeedsCellsLayout)
        {
            return;
        }

        AddInvalidationPathsForCells(true, _invalidatedPaths, VisibleCells, cell => IndexPathForCell((UICollectionViewCell)cell));
        AddInvalidationPathsForCells(_layoutInfo.HasGlobalHeader, _invalidatedGlobalHeaders, GetVisibleSupplementaryViews(VirtualScrollPlatformLayoutFactory.NSElementKindGlobalHeader), CreateGetIndexPathForSupplementaryView(VirtualScrollPlatformLayoutFactory.NSElementKindGlobalHeader));
        AddInvalidationPathsForCells(_layoutInfo.HasGlobalFooter, _invalidatedGlobalFooters, GetVisibleSupplementaryViews(VirtualScrollPlatformLayoutFactory.NSElementKindGlobalFooter), CreateGetIndexPathForSupplementaryView(VirtualScrollPlatformLayoutFactory.NSElementKindGlobalFooter));
        AddInvalidationPathsForCells(_layoutInfo.HasSectionHeader, _invalidatedSectionHeaders, GetVisibleSupplementaryViews(VirtualScrollPlatformLayoutFactory.NSElementKindSectionHeader), CreateGetIndexPathForSupplementaryView(VirtualScrollPlatformLayoutFactory.NSElementKindSectionHeader));
        AddInvalidationPathsForCells(_layoutInfo.HasSectionFooter, _invalidatedSectionFooters, GetVisibleSupplementaryViews(VirtualScrollPlatformLayoutFactory.NSElementKindSectionFooter), CreateGetIndexPathForSupplementaryView(VirtualScrollPlatformLayoutFactory.NSElementKindSectionFooter));

        var context = new UICollectionViewLayoutInvalidationContext();
        var invalidatedCount = 
            AddPathsToInvalidationContext(context, _invalidatedPaths, null) +
            AddPathsToInvalidationContext(context, _invalidatedGlobalHeaders, VirtualScrollPlatformLayoutFactory.NSElementKindGlobalHeader) +
            AddPathsToInvalidationContext(context, _invalidatedGlobalFooters, VirtualScrollPlatformLayoutFactory.NSElementKindGlobalFooter) +
            AddPathsToInvalidationContext(context, _invalidatedSectionHeaders, VirtualScrollPlatformLayoutFactory.NSElementKindSectionHeader) +
            AddPathsToInvalidationContext(context, _invalidatedSectionFooters, VirtualScrollPlatformLayoutFactory.NSElementKindSectionFooter);

        if (invalidatedCount > 0)
        {
            CollectionViewLayout.InvalidateLayout(context);
        }

        cellsLayoutController.SetCellLayoutCompleted();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddPathsToInvalidationContext(UICollectionViewLayoutInvalidationContext context, ICollection<NSIndexPath> paths, NSString? supplementaryKind)
    {
        if (paths.Count == 0)
        {
            return 0;
        }

        if (supplementaryKind is not null)
        {
            context.InvalidateSupplementaryElements(supplementaryKind, [..paths]);
        }
        else
        {
            context.InvalidateItems([..paths]);
        }

        var count = paths.Count;
        paths.Clear();
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Func<UICollectionReusableView, NSIndexPath?> CreateGetIndexPathForSupplementaryView(NSString kind)
    {
        if (OperatingSystem.IsIOSVersionAtLeast(18) || OperatingSystem.IsMacCatalystVersionAtLeast(18))
        {
            return GetIndexPathForSupplementaryView;
        }
        
        var allVisibleGlobalHeaders = GetIndexPathsForVisibleSupplementaryElements(kind);
        return cell =>
        {

            var path = allVisibleGlobalHeaders.Select<NSIndexPath, (NSIndexPath? IndexPath, UICollectionReusableView? Cell)?>(path => (path, GetSupplementaryView(kind, path)))
                                              .FirstOrDefault(t => t.HasValue && ReferenceEquals(t.Value.Cell, cell))
                                              ?.IndexPath;
            
            return path;
        };
    }

    private void AddInvalidationPathsForCells(bool enabled, List<NSIndexPath> indexPathsToBeInvalidated, IEnumerable<UICollectionReusableView> cells, Func<UICollectionReusableView, NSIndexPath?> pathGetter)
    {
        if (!enabled) 
        {
            return;
        }

        var cellsNeedingMeasure = cells
                    .OfType<VirtualScrollCell>()
                    .Where(cell => cell is { NeedsMeasure: true });

        foreach (var cell in cellsNeedingMeasure)
        {
            var indexPath = pathGetter(cell);
            if (indexPath is not null)
            {
                indexPathsToBeInvalidated.Add(indexPath);
            }
        }
    }

    void IVirtualScrollCellsLayoutController.SetCellLayoutCompleted() => _needsCellsLayout = false;

    /// <inheritdoc/>
    public override UICollectionReusableView DequeueReusableCell(NSString reuseIdentifier, NSIndexPath indexPath)
    {
        var cell = base.DequeueReusableCell(reuseIdentifier, indexPath);
        if (cell is VirtualScrollCell { UseCount: 0 } virtualCell)
        {
            _cellManager.TrackCell(virtualCell);
        }

        return cell;
    }

    /// <inheritdoc/>
    public override UICollectionReusableView DequeueReusableSupplementaryView(NSString kind, NSString identifier, NSIndexPath indexPath)
    {
        var cell = base.DequeueReusableSupplementaryView(kind, identifier, indexPath);
        if (cell is VirtualScrollCell { UseCount: 0 } virtualCell)
        {
            _cellManager.TrackCell(virtualCell);
        }

        return cell;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _cellManager.Dispose();
        }
    }

#if NET10_0_OR_GREATER
    private bool _invalidateParentWhenMovedToWindow;

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow() => _invalidateParentWhenMovedToWindow = true;

    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        SetNeedsLayout();
        return !isPropagating;
    }

    /// <inheritdoc/>
    public override void MovedToWindow()
    {
        base.MovedToWindow();

        if (_invalidateParentWhenMovedToWindow)
        {
            _invalidateParentWhenMovedToWindow = false;
            _invalidateAncestorsMeasuresMethodInfo(this);
        }
    }
    
    private static readonly Action<UIView> _invalidateAncestorsMeasuresMethodInfo = typeof(ViewExtensions)
                                                                                    .GetMethod("InvalidateAncestorsMeasures", BindingFlags.Static | BindingFlags.NonPublic)!
                                                                                    .CreateDelegate<Action<UIView>>();
#endif
}
