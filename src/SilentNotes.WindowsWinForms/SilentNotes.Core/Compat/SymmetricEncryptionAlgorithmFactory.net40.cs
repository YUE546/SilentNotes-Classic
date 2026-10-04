// Compat copy of SilentNotes.AllPlatforms\Crypto\SymmetricEncryption\SymmetricEncryptionAlgorithmFactory.cs
// for .NET 4.0. The xchacha20_poly1305 case is removed: BouncyCastle 1.8.9 (the newest
// BouncyCastle running on net40) predates XChaCha20. The default algorithm of the cryptor is
// switched to aes_gcm accordingly; existing xchacha20 ciphertexts require a one-time data
// migration before using the net40 build.
using System;

namespace SilentNotes.Crypto.SymmetricEncryption
{
    /// <summary>
    /// Factory to create the correct implementation of the encryption algorithm, from its name.
    /// </summary>
    public class SymmetricEncryptionAlgorithmFactory
    {
        /// <summary>
        /// Factory to create the correct implementation of the encryption algorithm, from its name.
        /// </summary>
        /// <param name="algorithmName">Name of the encryptor.</param>
        /// <returns>Instance of the given encryptor.</returns>
        public ISymmetricEncryptionAlgorithm CreateAlgorithm(string algorithmName)
        {
            // Add other algorithms if necessary
            switch (algorithmName)
            {
                case BouncyCastleAesGcm.CryptoAlgorithmName:
                    return new BouncyCastleAesGcm();
                case BouncyCastleTwofishGcm.CryptoAlgorithmName:
                    return new BouncyCastleTwofishGcm();
                default:
                    throw new CryptoException(string.Format("Unknown encryption algorithm '{0}'", algorithmName));
            }
        }
    }
}
