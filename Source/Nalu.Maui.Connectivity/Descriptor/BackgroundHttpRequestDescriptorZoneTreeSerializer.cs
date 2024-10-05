namespace Nalu;

using System.Security.Cryptography;
using System.Text.Json;
using Tenray.ZoneTree.Serializers;

internal class BackgroundHttpRequestDescriptorZoneTreeSerializer(SymmetricAlgorithm algorithm)
    : ISerializer<BackgroundHttpRequestDescriptor>
{
    private readonly ICryptoTransform _encryptor = algorithm.CreateEncryptor();
    private readonly ICryptoTransform _decryptor = algorithm.CreateDecryptor();

    public BackgroundHttpRequestDescriptor Deserialize(Memory<byte> bytes)
    {
        var byteArray = bytes.ToArray();
        return (BackgroundHttpRequestDescriptor)JsonSerializer.Deserialize(
            _decryptor.TransformFinalBlock(byteArray, 0, byteArray.Length),
            typeof(BackgroundHttpRequestDescriptor),
            BackgroundHttpRequestDescriptorJsonContext.Default)!;
    }

    public Memory<byte> Serialize(in BackgroundHttpRequestDescriptor entry)
    {
        var byteArray = JsonSerializer.SerializeToUtf8Bytes(
            entry,
            typeof(BackgroundHttpRequestDescriptor),
            BackgroundHttpRequestDescriptorJsonContext.Default);
        return _encryptor.TransformFinalBlock(byteArray, 0, byteArray.Length);
    }
}
