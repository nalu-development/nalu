using CoreGraphics;
using Foundation;
using UIKit;

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
{
    private readonly VirtualScrollCellManager<VirtualScrollCell> _cellManager = new(cell => cell.VirtualView);
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
    public VirtualScrollCollectionView(CGRect frame, UICollectionViewLayout layout) : base(frame, layout)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        AutomaticallyAdjustsScrollIndicatorInsets = true;
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

        var needingMeasureCells = VisibleCells.OfType<VirtualScrollCell>()
                                              .Where(cell => cell is { NeedsMeasure: true });

        foreach (var cell in needingMeasureCells)
        {
            var indexPath = IndexPathForCell(cell);
            if (indexPath is not null)
            {
                _invalidatedPaths.Add(indexPath);
            }
        }
        
        if (_invalidatedPaths.Count > 0)
        {
            var context = new UICollectionViewLayoutInvalidationContext();
            context.InvalidateItems([.._invalidatedPaths]);
            _invalidatedPaths.Clear();

            CollectionViewLayout.InvalidateLayout(context);
        }

        cellsLayoutController.SetCellLayoutCompleted();
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
}
