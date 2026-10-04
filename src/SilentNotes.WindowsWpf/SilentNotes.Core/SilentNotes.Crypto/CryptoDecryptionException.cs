using System;

namespace SilentNotes.Crypto;

public class CryptoDecryptionException : CryptoException
{
	public CryptoDecryptionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
