using System;

namespace VanillaCloudStorageClient;

public class CloudStorageException : Exception
{
	public CloudStorageException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
