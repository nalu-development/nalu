using WGrid = Microsoft.UI.Xaml.Controls.Grid;
using Size = Windows.Foundation.Size;

namespace Nalu;

internal partial class VirtualScrollPlatformView : WGrid
{
    public Size LastMeasureConstraint { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        LastMeasureConstraint = availableSize;
        var measureOverride = base.MeasureOverride(availableSize);

        return measureOverride;
    }
}
