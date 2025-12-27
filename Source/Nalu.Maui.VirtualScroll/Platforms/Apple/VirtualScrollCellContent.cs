using UIKit;

namespace Nalu;

internal class VirtualScrollCellContent : UIView
{
    // See https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/iOS/ViewExtensions.cs#L1023
    private const nint _nativeViewControlledByCrossPlatformLayout = 0x63D2A1;

    public bool NeedsMeasure { get; set; }

    public VirtualScrollCellContent()
    {
        Tag = _nativeViewControlledByCrossPlatformLayout;
    }
    
    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();
        NeedsMeasure = true;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        NeedsMeasure = false;
    }
}
