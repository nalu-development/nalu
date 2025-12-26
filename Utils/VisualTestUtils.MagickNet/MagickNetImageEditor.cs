using ImageMagick;

namespace VisualTestUtils.MagickNet;

public class MagickNetImageEditor : IImageEditor
{
    private readonly MagickImage _magickImage;

    public MagickNetImageEditor(ImageSnapshot imageSnapshot)
    {
        _magickImage = new MagickImage(imageSnapshot.Data);
    }

    public void Crop(int x, int y, uint width, uint height)
    {
        _magickImage.Crop(new MagickGeometry(x, y, width, height));
        _magickImage.ResetPage();
    }

    public (uint width, uint height) GetSize() =>
        (_magickImage.Width, _magickImage.Height);

    public ImageSnapshot GetUpdatedImage()
    {
        var format = _magickImage.Format switch
        {
            MagickFormat.Png => ImageSnapshotFormat.Png,
            MagickFormat.Jpeg => ImageSnapshotFormat.Jpeg,
            _ => throw new NotSupportedException($"Unexpected image format: {_magickImage.Format}")
        };

        return new ImageSnapshot(_magickImage.ToByteArray(), format);
    }
}
