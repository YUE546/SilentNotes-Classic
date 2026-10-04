using System;

namespace SilentNotes.Crypto;

public class CryptoException : Exception
{
	public CryptoException()
	{
	}

	public CryptoException(string message)
		: base(message)
	{
	}

	public CryptoException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
