using System.Security;
using SilentNotes.Crypto.KeyDerivation;

namespace SilentNotes.Crypto;

public interface ICryptor
{
	string PackageName { get; }

	byte[] Encrypt(byte[] message, SecureString password, KeyDerivationCostType costType, string encryptorName, string kdfName, string compression = null);

	byte[] Encrypt(byte[] message, byte[] key, string encryptorName, string compression = null);

	byte[] Decrypt(byte[] packedCipher, SecureString password, out bool needsReEncryption);

	byte[] Decrypt(byte[] packedCipher, byte[] key);
}
