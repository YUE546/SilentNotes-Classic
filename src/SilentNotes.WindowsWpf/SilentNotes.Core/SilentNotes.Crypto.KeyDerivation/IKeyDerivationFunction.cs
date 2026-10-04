using System.Security;

namespace SilentNotes.Crypto.KeyDerivation;

public interface IKeyDerivationFunction
{
	string Name { get; }

	int ExpectedSaltSizeBytes { get; }

	byte[] DeriveKeyFromPassword(SecureString password, int expectedKeySizeBytes, byte[] salt, string cost);

	string RecommendedCost(KeyDerivationCostType costType);

	bool NeedsRehashForHighCost(string cost);
}
