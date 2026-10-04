namespace SilentNotes.Models;

public class SafeOpenResult
{
	public SafeModel Safe { get; }

	public byte[] Key { get; }

	public bool NeedsReEncryption { get; }

	public SafeOpenResult(SafeModel safe, byte[] key, bool needsReEncryption)
	{
		Safe = safe;
		Key = key;
		NeedsReEncryption = needsReEncryption;
	}
}
