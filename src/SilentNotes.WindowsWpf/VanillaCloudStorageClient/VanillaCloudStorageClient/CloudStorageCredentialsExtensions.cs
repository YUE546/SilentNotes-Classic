using System;

namespace VanillaCloudStorageClient;

public static class CloudStorageCredentialsExtensions
{
	public static bool AreEqualOrNull(this CloudStorageCredentials credentials1, CloudStorageCredentials credentials2)
	{
		if (credentials1 == null && credentials2 == null)
		{
			return true;
		}
		return credentials1 != null && credentials2 != null && string.Equals(credentials1.CloudStorageId, credentials2.CloudStorageId, StringComparison.InvariantCultureIgnoreCase) && credentials1.Token.AreEqualOrNull(credentials2.Token) && credentials1.Url == credentials2.Url && credentials1.Username == credentials2.Username && credentials1.Secure == credentials2.Secure && credentials1.Password.AreEqual(credentials2.Password);
	}

	public static void ThrowIfInvalid(this CloudStorageCredentials credentials, CloudStorageCredentialsRequirements requirements, bool allowAnonymous = false)
	{
		if (credentials == null)
		{
			throw new InvalidParameterException("CloudStorageCredentials");
		}
		if (requirements.HasRequirement(CloudStorageCredentialsRequirements.Token) && credentials.Token == null)
		{
			throw new InvalidParameterException(string.Format("{0}.{1}", "CloudStorageCredentials", "Token"));
		}
		if (requirements.HasRequirement(CloudStorageCredentialsRequirements.Url) && string.IsNullOrWhiteSpace(credentials.Url))
		{
			throw new InvalidParameterException(string.Format("{0}.{1}", "CloudStorageCredentials", "Url"));
		}
		if (!allowAnonymous || !string.IsNullOrEmpty(credentials.Username))
		{
			if (requirements.HasRequirement(CloudStorageCredentialsRequirements.Username) && string.IsNullOrWhiteSpace(credentials.Username))
			{
				throw new InvalidParameterException(string.Format("{0}.{1}", "CloudStorageCredentials", "Username"));
			}
			if (requirements.HasRequirement(CloudStorageCredentialsRequirements.Password) && credentials.Password == null)
			{
				throw new InvalidParameterException(string.Format("{0}.{1}", "CloudStorageCredentials", "Password"));
			}
		}
	}
}
