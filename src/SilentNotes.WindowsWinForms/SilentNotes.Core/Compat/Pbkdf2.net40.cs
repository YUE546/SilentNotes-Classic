// Compat copy of SilentNotes.AllPlatforms\Crypto\KeyDerivation\Pbkdf2.cs for .NET 4.0.
// The original calls Rfc2898DeriveBytes.Pbkdf2(...) (static, .NET 6+ only). This copy uses
// the classic Rfc2898DeriveBytes constructor, which implements the identical
// PBKDF2-HMAC-SHA1 algorithm, so ciphertexts remain byte-for-byte compatible.
using System;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using VanillaCloudStorageClient;

namespace SilentNotes.Crypto.KeyDerivation
{
    /// <summary>
    /// Implementation of the <see cref="IKeyDerivationFunction"/> interface, implementing the
    /// PBKDF2 algorithm with SHA1 and HMAC.
    /// </summary>
    public class Pbkdf2 : IKeyDerivationFunction
    {
        /// <summary>The name of the PBKDF2 key derivation function.</summary>
        public const string CryptoKdfName = "pbkdf2";

        /// <summary>
        /// ~750ms on a mid-range mobile device.
        /// </summary>
        private const int HighCostIterations = 25000;

        private const int SaltSizeBytes = 16; // 128 bits

        /// <inheritdoc />
        public string Name
        {
            get { return CryptoKdfName; }
        }

        /// <inheritdoc />
        public byte[] DeriveKeyFromPassword(SecureString password, int expectedKeySizeBytes, byte[] salt, string cost)
        {
            if ((password == null) || (password.Length == 0))
                throw new CryptoException("The password cannot be empty.");
            int iterations;
            if (!int.TryParse(cost, NumberStyles.None, CultureInfo.InvariantCulture, out iterations))
                throw new CryptoException("The cost parameter has an invalid format.");
            if (iterations < 1)
                throw new CryptoException("The cost factor is too small.");

            byte[] binaryPassword = SecureStringExtensions.SecureStringToBytes(password, Encoding.UTF8);
            try
            {
                Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(binaryPassword, salt, iterations);
                byte[] result = rfc2898.GetBytes(expectedKeySizeBytes);
                return result;
            }
            finally
            {
                CryptoUtils.CleanArray(binaryPassword);
            }
        }

        /// <inheritdoc />
        public int ExpectedSaltSizeBytes
        {
            get { return SaltSizeBytes; }
        }

        /// <inheritdoc />
        public string RecommendedCost(KeyDerivationCostType costType)
        {
            switch (costType)
            {
                case KeyDerivationCostType.Low:
                    return "1500";
                case KeyDerivationCostType.High:
                    return HighCostIterations.ToString();
                default:
                    throw new ArgumentOutOfRangeException("costType");
            }
        }

        /// <inheritdoc/>
        public bool NeedsRehashForHighCost(string cost)
        {
            int iterations;
            if (!int.TryParse(cost, NumberStyles.None, CultureInfo.InvariantCulture, out iterations))
                throw new CryptoException("The cost parameter has an invalid format.");
            return HighCostIterations > iterations;
        }
    }
}
