namespace VanillaCloudStorageClient.OAuth2;

public static class OAuth2ConfigExtensions
{
	public static void ThrowIfInvalidForAuthorizationRequest(this OAuth2Config requestParams)
	{
		if (requestParams == null)
		{
			throw new InvalidParameterException("OAuth2Config");
		}
		if (string.IsNullOrWhiteSpace(requestParams.AuthorizeServiceEndpoint))
		{
			throw new InvalidParameterException(string.Format("{0}.{1}", "OAuth2Config", "AuthorizeServiceEndpoint"));
		}
		if (string.IsNullOrWhiteSpace(requestParams.ClientId))
		{
			throw new InvalidParameterException(string.Format("{0}.{1}", "OAuth2Config", "ClientId"));
		}
		if (string.IsNullOrWhiteSpace(requestParams.RedirectUrl))
		{
			throw new InvalidParameterException(string.Format("{0}.{1}", "OAuth2Config", "RedirectUrl"));
		}
	}
}
