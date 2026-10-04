using System;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using VanillaCloudStorageClient;

namespace SilentNotes.Crypto.KeyDerivation;

public class Pbkdf2 : IKeyDerivationFunction
{
	public const string CryptoKdfName = "pbkdf2";

	private const int HighCostIterations = 25000;

	private const int SaltSizeBytes = 16;

	public string Name => "pbkdf2";

	public int ExpectedSaltSizeBytes => 16;

	public byte[] DeriveKeyFromPassword(SecureString password, int expectedKeySizeBytes, byte[] salt, string cost)
	{
		if (password == null || password.Length == 0)
		{
			throw new CryptoException("The password cannot be empty.");
		}
		if (!int.TryParse(cost, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			throw new CryptoException("The cost parameter has an invalid format.");
		}
		if (result < 1)
		{
			throw new CryptoException("The cost factor is too small.");
		}
		byte[] array = password.SecureStringToBytes(Encoding.UTF8);
		try
		{
			using Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(array, salt, result);
			return rfc2898DeriveBytes.GetBytes(expectedKeySizeBytes);
		}
		finally
		{
			CryptoUtils.CleanArray(array);
		}
	}

	public string RecommendedCost(KeyDerivationCostType costType)
	{
		return costType switch
		{
			KeyDerivationCostType.Low => "1500", 
			KeyDerivationCostType.High => 25000.ToString(), 
			_ => throw new ArgumentOutOfRangeException("costType"), 
		};
	}

	public bool NeedsRehashForHighCost(string cost)
	{
		if (!int.TryParse(cost, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			throw new CryptoException("The cost parameter has an invalid format.");
		}
		return 25000 > result;
	}
}
