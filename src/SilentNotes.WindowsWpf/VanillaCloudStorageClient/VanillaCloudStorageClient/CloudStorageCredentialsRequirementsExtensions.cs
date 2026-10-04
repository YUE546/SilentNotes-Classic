namespace VanillaCloudStorageClient;

public static class CloudStorageCredentialsRequirementsExtensions
{
	public static bool HasRequirement(this CloudStorageCredentialsRequirements requirements, CloudStorageCredentialsRequirements requirement)
	{
		return (requirements & requirement) == requirement;
	}
}
