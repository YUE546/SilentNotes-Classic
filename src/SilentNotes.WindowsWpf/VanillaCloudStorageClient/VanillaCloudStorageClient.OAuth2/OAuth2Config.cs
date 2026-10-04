namespace VanillaCloudStorageClient.OAuth2;

public class OAuth2Config
{
	public string AuthorizeServiceEndpoint { get; set; }

	public string TokenServiceEndpoint { get; set; }

	public string ClientId { get; set; }

	public string RedirectUrl { get; set; }

	public string Scope { get; set; }

	public AuthorizationFlow Flow { get; set; }

	public ClientSecretHandling ClientSecretHandling { get; set; }
}
