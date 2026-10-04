using System;

namespace VanillaCloudStorageClient;

public class ConnectionFailedException : CloudStorageException
{
	public ConnectionFailedException()
		: base("An error occured while connecting to the server", null)
	{
	}

	public ConnectionFailedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	public ConnectionFailedException(Exception innerException)
		: base("An error occured while connecting to the server", innerException)
	{
	}
}
