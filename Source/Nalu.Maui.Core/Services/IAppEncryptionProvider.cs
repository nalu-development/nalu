namespace Nalu;

using System.Security.Cryptography;

/// <summary>
/// Provides the app encryption algorithm.
/// </summary>
public interface IAppEncryptionProvider
{
    /// <summary>
    /// Provides the app encryption algorithm.
    /// </summary>
    SymmetricAlgorithm GetSymmetricAlgorithm();
}
