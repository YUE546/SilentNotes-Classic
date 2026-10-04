namespace VanillaCloudStorageClient.OAuth2;

public enum AuthorizationResponseError
{
	Unknown,
	InvalidRequest,
	UnauthorizedClient,
	AccessDenied,
	UnsupportedResponseType,
	InvalidScope,
	ServerError,
	TemporarilyUnavailable
}
