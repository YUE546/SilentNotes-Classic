namespace SilentNotes.Crypto.KeyDerivation;

public class KeyDerivationFactory
{
	public IKeyDerivationFunction CreateKdf(string kdfName)
	{
		if (!(kdfName == "pbkdf2"))
		{
			if (kdfName == "argon2id")
			{
				return new BouncyCastleArgon2();
			}
			throw new CryptoException($"Unknown key derivation function '{kdfName}'");
		}
		return new Pbkdf2();
	}
}
