namespace VisualTestUtils;

public class ImageSnapshot
{
    public ImageSnapshot(byte[] data, ImageSnapshotFormat format)
    {
        Data = data;
        Format = format;
    }

    public ImageSnapshot(string path)
    {
        Data = File.ReadAllBytes(path);
        Format = ImageSnapshotFormatExtensions.GetImageFormat(path);
    }

    /// <summary>
    /// Gets image data as bytes, in the associated image format.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets image format.
    /// </summary>
    public ImageSnapshotFormat Format { get; }

    /// <summary>
    /// Gets the full file path that should be used for the image, given a directory and file name base,
    /// ensuring the extension is correct.
    /// </summary>
    /// <param name="directory">Directory where the image snapshot will be stored.</param>
    /// <param name="fileNameBase">File name base for the image snapshot.</param>
    /// <returns>Full file path for the image snapshot file.</returns>
    public string GetFilePath(string directory, string fileNameBase) =>
        Path.Combine(directory, fileNameBase + Format.GetFileExtension());

    /// <summary>
    /// Saves the image to the specified directory with the specified file name base.
    /// </summary>
    /// <param name="directory">Directory to save the image snapshot.</param>
    /// <param name="fileNameBase">File name base for the saved image snapshot.</param>
    public void Save(string directory, string fileNameBase)
    {
        var filePath = GetFilePath(directory, fileNameBase);
        File.WriteAllBytes(filePath, Data);
    }
}
