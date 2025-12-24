namespace VisualTestUtils;

public class ImageSizeDifference : ImageDifference
{
    private readonly uint _baselineWidth;
    private readonly uint _baselineHeight;
    private readonly uint _actualWidth;
    private readonly uint _actualHeight;

    public ImageSizeDifference(uint baselineWidth, uint baselineHeight, uint actualWidth, uint actualHeight)
    {
        _baselineWidth = baselineWidth;
        _baselineHeight = baselineHeight;
        _actualWidth = actualWidth;
        _actualHeight = actualHeight;
    }

    public override string Description =>
        $"size differs - baseline is {_baselineWidth}x{_baselineHeight} pixels, actual is {_actualWidth}x{_actualHeight} pixels";

    public static ImageSizeDifference? Compare(uint baselineWidth, uint baselineHeight, uint actualWidth, uint actualHeight) =>
        baselineWidth != actualWidth || baselineHeight != actualHeight
            ? new ImageSizeDifference(baselineWidth, baselineHeight, actualWidth, actualHeight)
            : null;
}
