using System;

namespace VanillaCloudStorageClient.OAuth2;

public static class AuthorizationResponseErrorExtensions
{
	public static AuthorizationResponseError? StringToAuthorizationResponseError(string error)
	{
		if (string.IsNullOrWhiteSpace(error))
		{
			return null;
		}
		error = error.Replace("_", string.Empty);
		if (Enum.TryParse<AuthorizationResponseError>(error, ignoreCase: true, out var result))
		{
			return result;
		}
		return AuthorizationResponseError.Unknown;
	}
}
