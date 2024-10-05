namespace Nalu;

using System.Security.Cryptography;

#pragma warning disable VSTHRD002

/// <summary>
/// A provider for encryption.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AppEncryptionProvider"/> class.
/// </remarks>
/// <param name="secureStorage">The secure storage used to store the key.</param>
public sealed class AppEncryptionProvider(ISecureStorage secureStorage) : IAppEncryptionProvider
{
    private readonly SymmetricAlgorithm _symmetricAlgorithm = GetOrCreateAes(secureStorage);

    /// <inheritdoc />
    public SymmetricAlgorithm GetSymmetricAlgorithm() => _symmetricAlgorithm;

    private static Aes GetOrCreateAes(ISecureStorage secureStorage)
    {
        var storageKey = "nalu.maui.aes";
        var aes = Aes.Create();

        var b64key = secureStorage.GetAsync(storageKey).GetAwaiter().GetResult();
        string key;
        string iv;
        if (b64key != null)
        {
            var parts = b64key.Split('|');
            key = parts[0];
            iv = parts[1];
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);
            return aes;
        }

        aes.GenerateKey();
        aes.GenerateIV();
        key = Convert.ToBase64String(aes.Key);
        iv = Convert.ToBase64String(aes.IV);
        b64key = $"{key}|{iv}";
        secureStorage.SetAsync(storageKey, b64key).GetAwaiter().GetResult();
        return aes;
    }
}
