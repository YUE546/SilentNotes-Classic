using System.Collections.Generic;
using System.Threading.Tasks;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class MurenaCloudStorageClient : WebdavCloudStorageClient
{
	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Username | CloudStorageCredentialsRequirements.Password | CloudStorageCredentialsRequirements.AcceptUnsafeCertificate;

	public MurenaCloudStorageClient(bool useSocketsForPropFind)
		: base(useSocketsForPropFind)
	{
	}

	public override Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		return base.UploadFileAsync(filename, fileContent, WithPredefinedUrl(credentials));
	}

	public override Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials)
	{
		return base.DownloadFileAsync(filename, WithPredefinedUrl(credentials));
	}

	public override Task DeleteFileAsync(string filename, CloudStorageCredentials credentials)
	{
		return base.DeleteFileAsync(filename, WithPredefinedUrl(credentials));
	}

	public override Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials)
	{
		return base.ListFileNamesAsync(WithPredefinedUrl(credentials));
	}

	private static CloudStorageCredentials WithPredefinedUrl(CloudStorageCredentials credentials)
	{
		if (credentials != null)
		{
			string text = credentials.Username;
			int num = text.IndexOf("@");
			if (num >= 0)
			{
				text = text.Remove(num);
			}
			credentials.Url = $"https://murena.io/remote.php/dav/files/{text}%40e.email";
			credentials.Username = text + "@e.email";
		}
		return credentials;
	}
}
