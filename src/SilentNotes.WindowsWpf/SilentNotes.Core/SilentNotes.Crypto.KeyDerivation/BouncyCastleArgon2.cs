using System;
using System.Security;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using VanillaCloudStorageClient;

namespace SilentNotes.Crypto.KeyDerivation;

public class BouncyCastleArgon2 : IKeyDerivationFunction
{
	public const string CryptoKdfName = "argon2id";

	private const int HighCostMemoryKib = 23000;

	private const int HighCostIterations = 2;

	private const int SaltSizeBytes = 16;

	public string Name => "argon2id";

	public int ExpectedSaltSizeBytes => 16;

	public byte[] DeriveKeyFromPassword(SecureString password, int expectedKeySizeBytes, byte[] salt, string cost)
	{
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		if (password == null || password.Length == 0)
		{
			throw new CryptoException("The password cannot be empty.");
		}
		if (!Argon2Cost.TryParse(cost, out var costParameters))
		{
			throw new CryptoException("Invalid cost parameter.");
		}
		if (expectedKeySizeBytes < 16)
		{
			throw new CryptoException("The key size is too small.");
		}
		if (salt.Length != 16)
		{
			throw new CryptoException("The salt size must be 16 bytes.");
		}
		if (costParameters.MemoryKib < 8)
		{
			throw new CryptoException("The memory cost factor is too small.");
		}
		if (costParameters.Iterations < 1)
		{
			throw new CryptoException("The iteration cost factor is too small.");
		}
		if (costParameters.Parallelism < 1)
		{
			throw new CryptoException("The parallelism cost factor is too small.");
		}
		byte[] array = password.SecureStringToBytes(Encoding.UTF8);
		try
		{
			Argon2Parameters val = new Argon2Parameters.Builder(Argon2Parameters.Argon2id).WithSalt(salt).WithMemoryAsKB(costParameters.MemoryKib).WithIterations(costParameters.Iterations)
				.WithParallelism(costParameters.Parallelism)
				.Build();
			Argon2BytesGenerator val2 = new Argon2BytesGenerator();
			val2.Init(val);
			byte[] array2 = new byte[expectedKeySizeBytes];
			val2.GenerateBytes(array, array2, 0, array2.Length);
			return array2;
		}
		finally
		{
			CryptoUtils.CleanArray(array);
		}
	}

	public string RecommendedCost(KeyDerivationCostType costType)
	{
		return (costType switch
		{
			KeyDerivationCostType.Low => new Argon2Cost
			{
				MemoryKib = 1000,
				Iterations = 1,
				Parallelism = 1
			}, 
			KeyDerivationCostType.High => new Argon2Cost
			{
				MemoryKib = 23000,
				Iterations = 2,
				Parallelism = 1
			}, 
			_ => throw new ArgumentOutOfRangeException("costType"), 
		}).Format();
	}

	public bool NeedsRehashForHighCost(string cost)
	{
		if (!Argon2Cost.TryParse(cost, out var costParameters))
		{
			throw new CryptoException("Invalid cost parameter.");
		}
		return 46000 > costParameters.MemoryKib * costParameters.Iterations;
	}
}

