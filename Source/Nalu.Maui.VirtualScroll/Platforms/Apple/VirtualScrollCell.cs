using System.Runtime.CompilerServices;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

internal sealed class VirtualScrollCell : UICollectionViewCell
{
    // ReSharper disable once InconsistentNaming
    public static readonly object InheritedBindingContext = new();
    
    private WeakReference<IView>? _weakVirtualView;
    private UIView? _nativeView;
    private uint _useCount;
    private bool _needsMeasure;
    private bool _readyForReuse = true;
    public IVirtualScrollLayout? ItemsLayout { get; set; }

    public NSString? SupplementaryType { get; set; }
    public NSIndexPath? IndexPath { get; set; }
    public bool NeedsMeasure => _needsMeasure && !_readyForReuse && ContentView.NeedsMeasure;
    public uint UseCount => _useCount;

    public IView? VirtualView
    {
        get => _weakVirtualView?.TryGetTarget(out var view) ?? false ? view : null;
        private set
        {
            if (value is null)
            {
                _weakVirtualView = null;

                return;
            }

            _weakVirtualView = new WeakReference<IView>(value);
        }
    }
    
    public UIView? NativeView
    {
        get => _nativeView;
        private set => _nativeView = value;
    }

    public new VirtualScrollCellContent ContentView { get; } 

    [Export("initWithFrame:")]
    public VirtualScrollCell(CGRect frame) : base(frame)
    {
        base.ContentView.RemoveFromSuperview();
        ContentView = new VirtualScrollCellContent();
        ContentView.TranslatesAutoresizingMaskIntoConstraints = false;
        AddSubview(ContentView);
        NSLayoutConstraint.ActivateConstraints([
            ContentView.TopAnchor.ConstraintEqualTo(TopAnchor),
            ContentView.BottomAnchor.ConstraintEqualTo(BottomAnchor),
            ContentView.LeadingAnchor.ConstraintEqualTo(LeadingAnchor),
            ContentView.TrailingAnchor.ConstraintEqualTo(TrailingAnchor)
        ]);
    }

    public override UICollectionViewLayoutAttributes PreferredLayoutAttributesFittingAttributes(UICollectionViewLayoutAttributes layoutAttributes)
    {
        _needsMeasure = false;

        if (VirtualView is not { } virtualView)
        {
            return base.PreferredLayoutAttributesFittingAttributes(layoutAttributes);
        }

        var attributesSize = layoutAttributes.Size;

        var layout = ItemsLayout ?? throw new InvalidOperationException("Layout is not set on VirtualScrollCell.");
        var isCarousel = layout is CarouselVirtualScrollLayout;
        var constraint = isCarousel
            ? new CGSize(attributesSize.Width, attributesSize.Height)
            : layout.Orientation == ItemsLayoutOrientation.Vertical
                ? new CGSize(attributesSize.Width, double.PositiveInfinity)
                : new CGSize(double.PositiveInfinity, attributesSize.Height);

        var measure = NativeView!.Hidden ? Size.Zero : virtualView.Measure(constraint.Width, constraint.Height);

        // Workaround to prevent a crash with a zero-width container during iPad split view / Stage Manager / rotation transitions.
        // If the container has zero width or height, supplementary views may be asked for preferred size,
        // leading to NSInternalInconsistencyException ("Preferred size is ZERO for auxiliary item").
        // We've already tried a workaround in VirtualScrollCollectionView.LayoutSubviews to avoid triggering layout when the collection view has no size,
        // but we want to make sure the cell doesn't crash if that somehow fails.
        var size = SupplementaryType is null
            ? layout.Orientation == ItemsLayoutOrientation.Vertical
                ? new CGSize(layoutAttributes.Frame.Width, measure.Height)
                : new CGSize(measure.Width, layoutAttributes.Frame.Height)
            : layout.Orientation == ItemsLayoutOrientation.Vertical
                ? new CGSize(Math.Max(1, layoutAttributes.Frame.Width), Math.Max(1, measure.Height))
                : new CGSize(Math.Max(1, measure.Width), Math.Max(1, layoutAttributes.Frame.Height));

        layoutAttributes.Frame = new CGRect(layoutAttributes.Frame.Location, size);

        return layoutAttributes;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ApplyBindings")]
    private static extern void ReapplyBindings(BindableObject bindable);

    public void SetupView(Func<object> viewFactory, IElementHandler handler, object? item)
    {
        var view = VirtualView;
        var isRebinding = true;
        if (view is null)
        {
            if (NativeView is not null)
            {
                throw new InvalidOperationException("NativeView exists but VirtualView is null.");
            }

            view = viewFactory() as IView ?? throw new InvalidOperationException("View factory did not return a valid IView.");
            VirtualView = view;
            isRebinding = false;
        }

        if (view is BindableObject bindable)
        {
            if (isRebinding)
            {
                if (ReferenceEquals(InheritedBindingContext, item))
                {
                    bindable.ClearValue(BindableObject.BindingContextProperty);
                }
                else
                {
                    bindable.BindingContext = item;
                }
            }
            else
            {
                if (!ReferenceEquals(InheritedBindingContext, item))
                {
                    bindable.BindingContext = item;
                }
            }
        }

        if (view is Element { Parent: null } viewElement && handler.VirtualView is Element parent)
        {
            parent.AddLogicalChild(viewElement);
        }

        if (NativeView is null)
        {
            var mauiContext = handler.MauiContext ?? throw new InvalidOperationException("MauiContext is null in VirtualScrollHandler.");
            var nativeView = view.ToPlatform(mauiContext);

            ContentView.AddSubview(nativeView);
            ContentView.UserInteractionEnabled = true;

            NativeView = nativeView;
        }

        _readyForReuse = false;
    }

    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();

        if (!_readyForReuse && ContentView.NeedsMeasure)
        {
            _needsMeasure = true;
            if (Superview is VirtualScrollCollectionView collectionView)
            {
                collectionView.SetNeedsCellsLayout();
            }
        }
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        if (VirtualView is { } virtualView)
        {
            var frame = ContentView.Bounds.ToRectangle();
            virtualView.Arrange(frame);
        }
    }

    public override void PrepareForReuse()
    {
        base.PrepareForReuse();

        IndexPath = null;
        SupplementaryType = null;

        unchecked
        {
            _useCount++;
        }

        _readyForReuse = true;
        _needsMeasure = false;
    }
}
