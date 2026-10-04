namespace SilentNotes.Crypto.SymmetricEncryption;

public interface ISymmetricEncryptionAlgorithm
{
	string Name { get; }

	int ExpectedKeySize { get; }

	int ExpectedNonceSize { get; }

	byte[] Encrypt(byte[] message, byte[] key, byte[] nonce);

	byte[] Decrypt(byte[] cipher, byte[] key, byte[] nonce);
}
