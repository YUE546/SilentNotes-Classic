using System;

namespace VanillaCloudStorageClient;

public static class CloudStorageTokenExtensions
{
	public static bool AreEqualOrNull(this CloudStorageToken token1, CloudStorageToken token2)
	{
		if (token1 == null && token2 == null)
		{
			return true;
		}
		return token1 != null && token2 != null && token1.AccessToken == token2.AccessToken && token1.RefreshToken == token2.RefreshToken && token1.ExpiryDate == token2.ExpiryDate;
	}

	public static void SetExpiryDateBySecondsFromNow(this CloudStorageToken token, int? seconds)
	{
		if (!seconds.HasValue)
		{
			token.ExpiryDate = null;
			return;
		}
		double num = Math.Min(60.0, (double)seconds.Value / 10.0);
		token.ExpiryDate = DateTime.UtcNow.AddSeconds((double)seconds.Value - num);
	}

	public static bool NeedsRefresh(this CloudStorageToken token)
	{
		if (token?.RefreshToken == null)
		{
			return false;
		}
		if (token.ExpiryDate.HasValue)
		{
			return token.ExpiryDate < DateTime.UtcNow;
		}
		return true;
	}
}
