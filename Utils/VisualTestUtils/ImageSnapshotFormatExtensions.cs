namespace VisualTestUtils;

public static class ImageSnapshotFormatExtensions
{
    public static string GetFileExtension(this ImageSnapshotFormat format) =>
        format switch
        {
            ImageSnapshotFormat.Png => ".png",
            ImageSnapshotFormat.Jpeg => ".jpg",
            _ => throw new InvalidOperationException($"Invalid ImageFormat value: {format}"),
        };

    public static ImageSnapshotFormat GetImageFormat(string filePath)
    {
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        if (fileExtension == ".png")
        {
            return ImageSnapshotFormat.Png;
        }
        else if (fileExtension == ".jpg" || fileExtension == ".jpeg")
        {
            return ImageSnapshotFormat.Jpeg;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported file type: {filePath}");
        }
    }
}
