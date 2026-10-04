namespace SilentNotes.Crypto;

public class CryptoHeader
{
	public const int NewestSupportedRevision = 2;

	public string PackageName { get; set; }

	public int Revision { get; set; }

	public string AlgorithmName { get; set; }

	public byte[] Nonce { get; set; }

	public string KdfName { get; set; }

	public byte[] Salt { get; set; }

	public string Cost { get; set; }

	public string Compression { get; set; }
}
