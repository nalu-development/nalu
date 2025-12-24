using ImageMagick;

namespace VisualTestUtils.MagickNet;

/// <summary>
/// Verify images using ImageMagick.
/// </summary>
public class MagickNetVisualComparer : IVisualComparer
{
    private readonly ErrorMetric _errorMetric;
    private readonly double _differenceThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagickNetVisualComparer"/> class.
    /// </summary>
    /// <param name="errorMetric">Error metric.</param>
    /// <param name="differenceThreshold">The maximum percent difference that is allowed between the baseline and actual snapshot images. Default value is .005, meaning the images must be at least 99.5% the same.).</param>
    public MagickNetVisualComparer(ErrorMetric errorMetric = ErrorMetric.Fuzz, double differenceThreshold = 0.005)
    {
        _errorMetric = errorMetric;
        _differenceThreshold = differenceThreshold;
    }

    /// <inheritdoc/>
    public ImageDifference? Compare(ImageSnapshot baselineImage, ImageSnapshot actualImage)
    {
        var magickBaselineImage = new MagickImage(baselineImage.Data);
        var magickActualImage = new MagickImage(actualImage.Data);

        var imageSizeDifference = ImageSizeDifference.Compare(magickBaselineImage.Width, magickBaselineImage.Height, magickActualImage.Width, magickActualImage.Height);
        if (imageSizeDifference != null)
        {
            return imageSizeDifference;
        }

        var distortionDifference = magickBaselineImage.Compare(magickActualImage, _errorMetric, Channels.Red);
        if (distortionDifference > _differenceThreshold)
        {
            return new ImagePercentageDifference(distortionDifference);
        }

        return null;
    }
}
