using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace SilentNotes.Crypto.SymmetricEncryption;

public class BouncyCastleTwofishGcm : ISymmetricEncryptionAlgorithm
{
	public const string CryptoAlgorithmName = "twofish_gcm";

	private const int NonceSizeBytes = 16;

	private const int KeySizeBytes = 32;

	private const int MacSizeBytes = 16;

	public string Name => "twofish_gcm";

	public int ExpectedKeySize => 32;

	public int ExpectedNonceSize => 16;

	public byte[] Encrypt(byte[] message, byte[] key, byte[] nonce)
	{
		return EncryptOrDecrypt(forEncryption: true, message, key, nonce);
	}

	public byte[] Decrypt(byte[] cipher, byte[] key, byte[] nonce)
	{
		return EncryptOrDecrypt(forEncryption: false, cipher, key, nonce);
	}

	private byte[] EncryptOrDecrypt(bool forEncryption, byte[] data, byte[] key, byte[] nonce)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		if (ExpectedKeySize != key.Length)
		{
			throw new CryptoException("Invalid key size");
		}
		if (nonce.Length < 12)
		{
			throw new CryptoException("Invalid nonce size");
		}
		ICipherParameters val = (ICipherParameters)new AeadParameters(new KeyParameter(key), 128, nonce, (byte[])null);
		IAeadBlockCipher val2 = (IAeadBlockCipher)new GcmBlockCipher((IBlockCipher)new TwofishEngine());
		((IAeadCipher)val2).Init(forEncryption, val);
		byte[] array = new byte[((IAeadCipher)val2).GetOutputSize(data.Length)];
		int num = ((IAeadCipher)val2).ProcessBytes(data, 0, data.Length, array, 0);
		((IAeadCipher)val2).DoFinal(array, num);
		return array;
	}
}
