namespace SilentNotes.Crypto.SymmetricEncryption;

public class SymmetricEncryptionAlgorithmFactory
{
	public ISymmetricEncryptionAlgorithm CreateAlgorithm(string algorithmName)
	{
		return algorithmName switch
		{
			"xchacha20_poly1305" => new BouncyCastleXChaCha20(), 
			"aes_gcm" => new BouncyCastleAesGcm(), 
			"twofish_gcm" => new BouncyCastleTwofishGcm(), 
			_ => throw new CryptoException($"Unknown encryption algorithm '{algorithmName}'"), 
		};
	}
}
