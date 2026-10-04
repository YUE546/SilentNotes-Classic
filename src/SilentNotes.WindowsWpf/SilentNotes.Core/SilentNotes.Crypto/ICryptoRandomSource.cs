namespace SilentNotes.Crypto;

public interface ICryptoRandomSource
{
	byte[] GetRandomBytes(int numberOfBytes);
}
