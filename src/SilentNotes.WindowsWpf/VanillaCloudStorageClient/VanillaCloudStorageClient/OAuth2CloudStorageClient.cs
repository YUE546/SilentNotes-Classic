using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using VanillaCloudStorageClient.OAuth2;

namespace VanillaCloudStorageClient;

public abstract class OAuth2CloudStorageClient : CloudStorageClientBase, ICloudStorageClient, IOAuth2CloudStorageClient
{
	private class JsonTokenExchangeResponse
	{
		[JsonPropertyName("access_token")]
		public string AccessToken { get; set; }

		[JsonPropertyName("refresh_token")]
		public string RefreshToken { get; set; }

		[JsonPropertyName("expires_in")]
		public int? ExpiresIn { get; set; }
	}

	protected OAuth2Config Config { get; private set; }

	protected OAuth2CloudStorageClient(OAuth2Config config)
	{
		Config = config;
	}

	public virtual string BuildAuthorizationRequestUrl(string state, string codeVerifier)
	{
		return OAuth2Utils.BuildAuthorizationRequestUrl(Config, state, codeVerifier);
	}

	public virtual async Task<CloudStorageToken> FetchTokenAsync(string redirectedUrl, string state, string codeVerifier)
	{
		if (string.IsNullOrWhiteSpace(redirectedUrl))
		{
			throw new ArgumentNullException("redirectedUrl");
		}
		if (string.IsNullOrWhiteSpace(state))
		{
			throw new ArgumentNullException("state");
		}
		AuthorizationResponse response = OAuth2Utils.ParseAuthorizationResponseUrl(redirectedUrl);
		if (!response.IsAccessGranted)
		{
			return null;
		}
		if (response.State != state)
		{
			throw new CloudStorageException("The authorization response has a wrong state, this indicates a hacking attempt.", null);
		}
		AuthorizationFlow flow;
		if (!string.IsNullOrWhiteSpace(response.Token))
		{
			flow = AuthorizationFlow.Token;
		}
		else
		{
			if (string.IsNullOrWhiteSpace(response.Code))
			{
				throw new CloudStorageException("The authorization response is neither form a token-flow, nor from a code-flow request.", null);
			}
			flow = AuthorizationFlow.Code;
		}
		return flow switch
		{
			AuthorizationFlow.Token => new CloudStorageToken
			{
				AccessToken = response.Token,
				ExpiryDate = null,
				RefreshToken = null
			}, 
			AuthorizationFlow.Code => await ExchangeCodeForTokenAsync(response.Code, codeVerifier), 
			_ => null, 
		};
	}

	public virtual async Task<CloudStorageToken> RefreshTokenAsync(CloudStorageToken token)
	{
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		if (string.IsNullOrWhiteSpace(token.RefreshToken))
		{
			throw new ArgumentNullException(string.Format("{0}.{1}", "token", "RefreshToken"));
		}
		try
		{
			string clientSecret = ((Config.ClientSecretHandling == ClientSecretHandling.SendEmptyParam) ? string.Empty : null);
			JsonTokenExchangeResponse response = JsonSerializer.Deserialize<JsonTokenExchangeResponse>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PostUrlEncodedAsync(GetFlurl().Request(new object[1] { Config.TokenServiceEndpoint }), (object)new
			{
				refresh_token = token.RefreshToken,
				client_id = Config.ClientId,
				client_secret = clientSecret,
				grant_type = "refresh_token"
			}, (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
			string refreshToken = ((!string.IsNullOrEmpty(response.RefreshToken)) ? response.RefreshToken : token.RefreshToken);
			CloudStorageToken result = new CloudStorageToken
			{
				AccessToken = response.AccessToken,
				RefreshToken = refreshToken
			};
			result.SetExpiryDateBySecondsFromNow(response.ExpiresIn);
			return result;
		}
		catch (Exception ex)
		{
			if (await IsInvalidGrantException(ex))
			{
				throw new RefreshTokenExpiredException(ex);
			}
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex);
		}
	}

	protected virtual async Task<CloudStorageToken> ExchangeCodeForTokenAsync(string authorizationCode, string codeVerifier)
	{
		if (string.IsNullOrWhiteSpace(authorizationCode))
		{
			throw new InvalidParameterException("authorizationCode");
		}
		try
		{
			string clientSecret = ((Config.ClientSecretHandling == ClientSecretHandling.SendEmptyParam) ? string.Empty : null);
			JsonTokenExchangeResponse response = JsonSerializer.Deserialize<JsonTokenExchangeResponse>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PostUrlEncodedAsync(GetFlurl().Request(new object[1] { Config.TokenServiceEndpoint }), (object)new
			{
				code = authorizationCode,
				client_id = Config.ClientId,
				client_secret = clientSecret,
				redirect_uri = Config.RedirectUrl,
				grant_type = "authorization_code",
				code_verifier = codeVerifier
			}, (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
			CloudStorageToken result = new CloudStorageToken
			{
				AccessToken = response.AccessToken,
				RefreshToken = response.RefreshToken
			};
			result.SetExpiryDateBySecondsFromNow(response.ExpiresIn);
			return result;
		}
		catch (Exception catchedException)
		{
			throw CloudStorageClientBase.ConvertToCloudStorageException(catchedException);
		}
	}

	private static async Task<bool> IsInvalidGrantException(Exception ex)
	{
		HttpStatusCode[] possibleCodes = new HttpStatusCode[3]
		{
			HttpStatusCode.BadRequest,
			HttpStatusCode.Unauthorized,
			HttpStatusCode.Forbidden
		};
		FlurlHttpException ex2;
		FlurlHttpException flurlHttpException = (ex2 = (FlurlHttpException)(object)((ex is FlurlHttpException) ? ex : null));
		if (ex2 != null && flurlHttpException.StatusCode.HasValue && possibleCodes.Contains(flurlHttpException.GetHttpStatusCode()))
		{
			string jsonResponse = await flurlHttpException.GetResponseStringAsync();
			return jsonResponse != null && jsonResponse.IndexOf("invalid_grant", StringComparison.InvariantCultureIgnoreCase) > 0;
		}
		return false;
	}
}

