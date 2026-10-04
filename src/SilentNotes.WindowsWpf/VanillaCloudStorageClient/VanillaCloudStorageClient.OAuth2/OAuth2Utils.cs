using System;
using System.Security.Cryptography;
using System.Text;
using Flurl;

namespace VanillaCloudStorageClient.OAuth2;

public static class OAuth2Utils
{
	public static string BuildAuthorizationRequestUrl(OAuth2Config config, string stateParam, string codeVerifier)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		config.ThrowIfInvalidForAuthorizationRequest();
		Url val = new Url(config.AuthorizeServiceEndpoint).SetQueryParams((object)new
		{
			response_type = config.Flow.ToString().ToLowerInvariant(),
			client_id = config.ClientId,
			redirect_uri = config.RedirectUrl,
			scope = config.Scope,
			state = stateParam
		}, (NullValueHandling)1);
		if (!string.IsNullOrWhiteSpace(codeVerifier))
		{
			val.SetQueryParams((object)new
			{
				code_challenge = HashCodeVerifier(codeVerifier),
				code_challenge_method = "S256"
			}, (NullValueHandling)1);
		}
		return (string)val;
	}

	private static string HashCodeVerifier(string codeVerifier)
	{
		using SHA256 sHA = SHA256.Create();
		byte[] data = sHA.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
		return Base64UrlEncode(data);
	}

	private static string Base64UrlEncode(byte[] data)
	{
		string text = Convert.ToBase64String(data);
		text = text.Replace("+", "-");
		text = text.Replace("/", "_");
		return text.Replace("=", "");
	}

	public static AuthorizationResponse ParseAuthorizationResponseUrl(string redirectedUrl)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		if (string.IsNullOrWhiteSpace(redirectedUrl))
		{
			throw new ArgumentNullException("redirectedUrl");
		}
		Url val = new Url(redirectedUrl);
		if (string.IsNullOrWhiteSpace(val.Query))
		{
			val.Query = val.Fragment;
		}
		return new AuthorizationResponse
		{
			Token = val.QueryParams.FirstOrDefault("access_token")?.ToString(),
			Code = val.QueryParams.FirstOrDefault("code")?.ToString(),
			State = val.QueryParams.FirstOrDefault("state")?.ToString(),
			Error = AuthorizationResponseErrorExtensions.StringToAuthorizationResponseError(val.QueryParams.FirstOrDefault("error")?.ToString())
		};
	}
}

