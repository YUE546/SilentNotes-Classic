using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class GmxCloudStorageClient : WebdavCloudStorageClient
{
	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Username | CloudStorageCredentialsRequirements.Password;

	public GmxCloudStorageClient(bool useSocketsForPropFind)
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
			if (credentials.Username != null && credentials.Username.EndsWith("gmx.com", StringComparison.InvariantCultureIgnoreCase))
			{
				credentials.Url = "https://storage-file-eu.gmx.com";
			}
			else
			{
				credentials.Url = "https://webdav.mc.gmx.net";
			}
		}
		return credentials;
	}
}
