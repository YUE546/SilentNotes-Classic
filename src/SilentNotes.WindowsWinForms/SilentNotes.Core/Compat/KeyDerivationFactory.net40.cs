// Compat copy of SilentNotes.AllPlatforms\Crypto\KeyDerivation\KeyDerivationFactory.cs for .NET 4.0.
// The argon2id case is removed: no Argon2 implementation exists for .NET 4.0, the Windows
// clients are switched to pbkdf2 (same kdf name as the original upstream format). Existing
// argon2id ciphertexts require a one-time data migration before using the net40 build.
using System;

namespace SilentNotes.Crypto.KeyDerivation
{
    /// <summary>
    /// Factory to create a key derivation function.
    /// </summary>
    public class KeyDerivationFactory
    {
        /// <summary>
        /// Creates the correct implementation of the key-derivation-function from its name.
        /// </summary>
        /// <param name="kdfName">Name of the required key derivation function.</param>
        /// <returns>Instance of the given key derivation function.</returns>
        public IKeyDerivationFunction CreateKdf(string kdfName)
        {
            switch (kdfName)
            {
                // Add other algorithms if necessary
                case Pbkdf2.CryptoKdfName:
                    return new Pbkdf2();
                default:
                    throw new CryptoException(string.Format("Unknown key derivation function '{0}'", kdfName));
            }
        }
    }
}
