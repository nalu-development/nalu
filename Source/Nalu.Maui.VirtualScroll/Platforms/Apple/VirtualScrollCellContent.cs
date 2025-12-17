using UIKit;

namespace Nalu;

internal class VirtualScrollCellContent : UIView
{
    public bool NeedsMeasure { get; set; }
    
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
