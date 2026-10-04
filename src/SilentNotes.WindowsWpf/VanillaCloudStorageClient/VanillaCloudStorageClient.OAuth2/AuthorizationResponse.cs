namespace VanillaCloudStorageClient.OAuth2;

public class AuthorizationResponse
{
	public bool IsAccessGranted => !Error.HasValue;

	public string Token { get; set; }

	public string Code { get; set; }

	public string State { get; set; }

	public AuthorizationResponseError? Error { get; set; }
}
