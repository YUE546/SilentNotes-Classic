using System;

namespace VanillaCloudStorageClient;

public class RefreshTokenExpiredException : CloudStorageException
{
	public RefreshTokenExpiredException()
		: base("The OAuth2 server answered with a 'invalid_grant error'.", null)
	{
	}

	public RefreshTokenExpiredException(Exception innerException)
		: base("The OAuth2 server answered with a 'invalid_grant error'.", innerException)
	{
	}
}
