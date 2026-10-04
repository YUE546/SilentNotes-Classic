using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using VanillaCloudStorageClient.OAuth2;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class OnedriveCloudStorageClient : OAuth2CloudStorageClient, ICloudStorageClient, IOAuth2CloudStorageClient
{
	private class JsonUploadSession
	{
		[JsonPropertyName("uploadUrl")]
		public string UploadUrl { get; set; }
	}

	private class JsonFolderEntries
	{
		[JsonPropertyName("value")]
		public List<JsonFolderEntry> Entries { get; set; }

		[JsonPropertyName("@odata.nextLink")]
		public string NextLink { get; set; }
	}

	private class JsonFolderEntry
	{
		[JsonPropertyName("file")]
		public JsonFolderEntryFile File { get; set; }

		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }
	}

	private class JsonFolderEntryFile
	{
	}

	private const string AuthorizeUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";

	private const string TokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

	private const string UploadUrl = "https://graph.microsoft.com/v1.0/me/drive/items/{0}:/{1}:/createUploadSession";

	private const string DownloadUrl = "https://graph.microsoft.com/v1.0/me/drive/items/{0}:/{1}:/content";

	private const string DeleteUrl = "https://graph.microsoft.com/v1.0/me/drive/items/{0}:/{1}";

	private const string ListUrl = "https://graph.microsoft.com/v1.0/me/drive/items/{0}/children";

	private const string AppRootUrl = "https://graph.microsoft.com/v1.0/me/drive/special/approot";

	private string _appRootId;

	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Token;

	public OnedriveCloudStorageClient(string oauthClientId, string oauthRedirectUrl)
		: base(new OAuth2Config
		{
			AuthorizeServiceEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
			TokenServiceEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
			ClientId = oauthClientId,
			RedirectUrl = oauthRedirectUrl,
			Flow = AuthorizationFlow.Code,
			Scope = "offline_access Files.ReadWrite.AppFolder",
			ClientSecretHandling = ClientSecretHandling.DoNotSend
		})
	{
	}

	public override async Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			string url = $"https://graph.microsoft.com/v1.0/me/drive/items/{await InitializeAppRootFolderAsync(credentials.Token.AccessToken)}:/{Url.Encode(filename, false)}:/createUploadSession";
			byte[] requestBytes = Encoding.UTF8.GetBytes("{ \"item\": { \"@microsoft.graph.conflictBehavior\": \"replace\" } }");
			HttpContent sessionContent = (HttpContent)new ByteArrayContent(requestBytes);
			JsonUploadSession session = JsonSerializer.Deserialize<JsonUploadSession>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PostAsync(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { url }), credentials.Token.AccessToken), "Content-Type", (object)"application/json"), sessionContent, (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
			HttpContent content = (HttpContent)new ByteArrayContent(fileContent);
			await Flurl.Http.GeneratedExtensions.PutAsync(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithHeader<IFlurlRequest>(GetFlurl().Request(new object[1] { session.UploadUrl }), "Content-Length", (object)fileContent.Length), "Content-Range", (object)$"bytes 0-{fileContent.Length - 1}/{fileContent.Length}"), content, (HttpCompletionOption)0, default(CancellationToken));
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			string url = $"https://graph.microsoft.com/v1.0/me/drive/items/{await InitializeAppRootFolderAsync(credentials.Token.AccessToken)}:/{Url.Encode(filename, false)}:/content";
			return await ResponseExtensions.ReceiveBytes(Flurl.Http.GeneratedExtensions.GetAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { url }), credentials.Token.AccessToken), (HttpCompletionOption)0, default(CancellationToken)));
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task DeleteFileAsync(string filename, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			string url = $"https://graph.microsoft.com/v1.0/me/drive/items/{await InitializeAppRootFolderAsync(credentials.Token.AccessToken)}:/{Url.Encode(filename, false)}";
			await Flurl.Http.GeneratedExtensions.DeleteAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { url }), credentials.Token.AccessToken), (HttpCompletionOption)0, default(CancellationToken));
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			List<string> result = new List<string>();
			string url = $"https://graph.microsoft.com/v1.0/me/drive/items/{await InitializeAppRootFolderAsync(credentials.Token.AccessToken)}/children";
			while (url != null)
			{
				JsonFolderEntries entries = JsonSerializer.Deserialize<JsonFolderEntries>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { url }), credentials.Token.AccessToken), "$select", "name,file", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
				result.AddRange(from item in entries.Entries
					where item.File != null
					select item.Name);
				url = entries.NextLink;
			}
			return result;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	private async Task<string> FindAppRootIdAsync(string accessToken)
	{
		try
		{
			JsonFolderEntry entry = JsonSerializer.Deserialize<JsonFolderEntry>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://graph.microsoft.com/v1.0/me/drive/special/approot" }), accessToken), "$select", "id", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
			return entry.Id;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	private async Task<string> InitializeAppRootFolderAsync(string accessToken)
	{
		if (string.IsNullOrEmpty(_appRootId))
		{
			_appRootId = await FindAppRootIdAsync(accessToken);
		}
		return _appRootId;
	}
}

