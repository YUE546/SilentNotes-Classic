using System.Collections.Generic;
using System.Threading.Tasks;

namespace VanillaCloudStorageClient;

public interface ICloudStorageClient
{
	CloudStorageCredentialsRequirements CredentialsRequirements { get; }

	Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials);

	Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials);

	Task DeleteFileAsync(string filename, CloudStorageCredentials credentials);

	Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials);

	Task<bool> ExistsFileAsync(string filename, CloudStorageCredentials credentials);
}
