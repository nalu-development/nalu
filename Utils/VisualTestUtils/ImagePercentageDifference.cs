namespace VisualTestUtils;

public class ImagePercentageDifference : ImageDifference
{
    private readonly double _percentage;

    public ImagePercentageDifference(double percentage)
    {
        _percentage = percentage;
    }

    public override string Description =>
        $"{_percentage * 100.0:0.00}% difference";
}
