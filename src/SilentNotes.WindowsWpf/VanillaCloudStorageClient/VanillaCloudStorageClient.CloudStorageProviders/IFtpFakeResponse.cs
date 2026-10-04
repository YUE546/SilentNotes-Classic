namespace VanillaCloudStorageClient.CloudStorageProviders;

public interface IFtpFakeResponse
{
	string GetFakeServerResponseString(string url);

	byte[] GetFakeServerResponseBytes(string url);

	bool GetFakeServerExistsFile(string url);
}
