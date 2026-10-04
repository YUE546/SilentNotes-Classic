using System;

namespace VanillaCloudStorageClient;

public class AccessDeniedException : CloudStorageException
{
	public AccessDeniedException()
		: base("Access to the requested resource was denied.", null)
	{
	}

	public AccessDeniedException(Exception innerException)
		: base("Access to the requested resource was denied.", innerException)
	{
	}
}
