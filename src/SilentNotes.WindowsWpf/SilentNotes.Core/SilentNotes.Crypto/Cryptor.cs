using System;
using System.Security;
using SilentNotes.Crypto.KeyDerivation;
using SilentNotes.Crypto.SymmetricEncryption;
using SilentNotes.Workers;

namespace SilentNotes.Crypto;

public class Cryptor : ICryptor
{
	public const string CompressionGzip = "gzip";

	private const int MinPasswordLength = 5;

	private readonly ICryptoRandomSource _randomSource;

	public string PackageName { get; private set; }

	public Cryptor(string packageName, ICryptoRandomSource randomSource)
	{
		PackageName = packageName;
		_randomSource = randomSource;
	}

	public byte[] Encrypt(byte[] message, SecureString password, KeyDerivationCostType costType, string encryptorName, string kdfName, string compression = null)
	{
		if (message == null)
		{
			throw new ArgumentNullException("message");
		}
		ValidatePassword(password);
		if (_randomSource == null)
		{
			throw new ArgumentNullException("_randomSource");
		}
		if (string.IsNullOrWhiteSpace(encryptorName))
		{
			encryptorName = "xchacha20_poly1305";
		}
		if (string.IsNullOrWhiteSpace(kdfName))
		{
			encryptorName = "pbkdf2";
		}
		ISymmetricEncryptionAlgorithm symmetricEncryptionAlgorithm = new SymmetricEncryptionAlgorithmFactory().CreateAlgorithm(encryptorName);
		IKeyDerivationFunction keyDerivationFunction = new KeyDerivationFactory().CreateKdf(kdfName);
		CryptoHeader cryptoHeader = new CryptoHeader();
		cryptoHeader.PackageName = PackageName;
		cryptoHeader.AlgorithmName = symmetricEncryptionAlgorithm.Name;
		cryptoHeader.Nonce = _randomSource.GetRandomBytes(symmetricEncryptionAlgorithm.ExpectedNonceSize);
		cryptoHeader.KdfName = keyDerivationFunction.Name;
		cryptoHeader.Salt = _randomSource.GetRandomBytes(keyDerivationFunction.ExpectedSaltSizeBytes);
		cryptoHeader.Cost = keyDerivationFunction.RecommendedCost(costType);
		cryptoHeader.Compression = compression;
		try
		{
			if (string.Equals("gzip", cryptoHeader.Compression, StringComparison.InvariantCultureIgnoreCase))
			{
				message = CompressUtils.Compress(message);
			}
			byte[] key = keyDerivationFunction.DeriveKeyFromPassword(password, symmetricEncryptionAlgorithm.ExpectedKeySize, cryptoHeader.Salt, cryptoHeader.Cost);
			byte[] cipher = symmetricEncryptionAlgorithm.Encrypt(message, key, cryptoHeader.Nonce);
			return CryptoHeaderPacker.PackHeaderAndCypher(cryptoHeader, cipher);
		}
		catch (Exception innerException)
		{
			throw new CryptoException("An unexpected error occured, while encrypting the message.", innerException);
		}
	}

	public byte[] Encrypt(byte[] message, byte[] key, string encryptorName, string compression = null)
	{
		if (message == null)
		{
			throw new ArgumentNullException("message");
		}
		if (key == null)
		{
			throw new ArgumentNullException("key");
		}
		if (_randomSource == null)
		{
			throw new ArgumentNullException("_randomSource");
		}
		if (string.IsNullOrWhiteSpace(encryptorName))
		{
			encryptorName = "xchacha20_poly1305";
		}
		ISymmetricEncryptionAlgorithm symmetricEncryptionAlgorithm = new SymmetricEncryptionAlgorithmFactory().CreateAlgorithm(encryptorName);
		CryptoHeader cryptoHeader = new CryptoHeader();
		cryptoHeader.PackageName = PackageName;
		cryptoHeader.AlgorithmName = symmetricEncryptionAlgorithm.Name;
		cryptoHeader.Nonce = _randomSource.GetRandomBytes(symmetricEncryptionAlgorithm.ExpectedNonceSize);
		cryptoHeader.Compression = compression;
		try
		{
			if (string.Equals("gzip", cryptoHeader.Compression, StringComparison.InvariantCultureIgnoreCase))
			{
				message = CompressUtils.Compress(message);
			}
			byte[] key2 = CryptoUtils.TruncateKey(key, symmetricEncryptionAlgorithm.ExpectedKeySize);
			byte[] cipher = symmetricEncryptionAlgorithm.Encrypt(message, key2, cryptoHeader.Nonce);
			return CryptoHeaderPacker.PackHeaderAndCypher(cryptoHeader, cipher);
		}
		catch (Exception innerException)
		{
			throw new CryptoException("An unexpected error occured, while encrypting the message.", innerException);
		}
	}

	public byte[] Decrypt(byte[] packedCipher, SecureString password, out bool needsReEncryption)
	{
		if (packedCipher == null)
		{
			throw new ArgumentNullException("packedCipher");
		}
		CryptoHeaderPacker.UnpackHeaderAndCipher(packedCipher, PackageName, out var header, out var cipher);
		ISymmetricEncryptionAlgorithm symmetricEncryptionAlgorithm = new SymmetricEncryptionAlgorithmFactory().CreateAlgorithm(header.AlgorithmName);
		IKeyDerivationFunction keyDerivationFunction = new KeyDerivationFactory().CreateKdf(header.KdfName);
		needsReEncryption = keyDerivationFunction.NeedsRehashForHighCost(header.Cost);
		try
		{
			byte[] key = keyDerivationFunction.DeriveKeyFromPassword(password, symmetricEncryptionAlgorithm.ExpectedKeySize, header.Salt, header.Cost);
			byte[] array = symmetricEncryptionAlgorithm.Decrypt(cipher, key, header.Nonce);
			if (string.Equals("gzip", header.Compression, StringComparison.InvariantCultureIgnoreCase))
			{
				array = CompressUtils.Decompress(array);
			}
			return array;
		}
		catch (Exception innerException)
		{
			throw new CryptoDecryptionException("Could not decrypt cipher, probably because the key is wrong.", innerException);
		}
	}

	public byte[] Decrypt(byte[] packedCipher, byte[] key)
	{
		if (packedCipher == null)
		{
			throw new ArgumentNullException("packedCipher");
		}
		CryptoHeaderPacker.UnpackHeaderAndCipher(packedCipher, PackageName, out var header, out var cipher);
		ISymmetricEncryptionAlgorithm symmetricEncryptionAlgorithm = new SymmetricEncryptionAlgorithmFactory().CreateAlgorithm(header.AlgorithmName);
		try
		{
			byte[] key2 = CryptoUtils.TruncateKey(key, symmetricEncryptionAlgorithm.ExpectedKeySize);
			byte[] array = symmetricEncryptionAlgorithm.Decrypt(cipher, key2, header.Nonce);
			if (string.Equals("gzip", header.Compression, StringComparison.InvariantCultureIgnoreCase))
			{
				array = CompressUtils.Decompress(array);
			}
			return array;
		}
		catch (Exception innerException)
		{
			throw new CryptoDecryptionException("Could not decrypt cipher, probably because the key is wrong.", innerException);
		}
	}

	private void ValidatePassword(SecureString password)
	{
		if (password == null || password.Length < 5)
		{
			throw new CryptoException($"The password must be at least {5} characters in length.");
		}
	}
}
