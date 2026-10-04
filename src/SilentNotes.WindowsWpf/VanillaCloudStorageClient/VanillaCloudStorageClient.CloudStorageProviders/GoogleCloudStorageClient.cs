using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Flurl.Util;
using VanillaCloudStorageClient.OAuth2;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class GoogleCloudStorageClient : OAuth2CloudStorageClient, ICloudStorageClient, IOAuth2CloudStorageClient
{
	private class JsonFolderEntries
	{
		[JsonPropertyName("files")]
		public List<JsonFolderEntry> Entries { get; set; }

		[JsonPropertyName("nextPageToken")]
		public string Cursor { get; set; }

		[JsonPropertyName("incompleteSearch")]
		public bool IncompleteSearch { get; set; }
	}

	private class JsonFolderEntry
	{
		[JsonPropertyName("kind")]
		public string Kind { get; set; }

		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }
	}

	private const string AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";

	private const string TokenUrl = "https://www.googleapis.com/oauth2/v4/token";

	private const string UploadUrl = "https://www.googleapis.com/upload/drive/v3/files";

	private const string DownloadUrl = "https://www.googleapis.com/drive/v3/files";

	private const string DeleteUrl = "https://www.googleapis.com/drive/v3/files";

	private const string ListUrl = "https://www.googleapis.com/drive/v3/files";

	private const string DataFolder = "appDataFolder";

	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Token;

	public GoogleCloudStorageClient(string oauthClientId, string oauthRedirectUrl)
		: base(new OAuth2Config
		{
			AuthorizeServiceEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
			TokenServiceEndpoint = "https://www.googleapis.com/oauth2/v4/token",
			ClientId = oauthClientId,
			RedirectUrl = oauthRedirectUrl,
			Flow = AuthorizationFlow.Code,
			Scope = "https://www.googleapis.com/auth/drive.appdata",
			ClientSecretHandling = ClientSecretHandling.SendEmptyParam
		})
	{
	}

	public override async Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			string fileId = await FindFileIdAsync(credentials.Token.AccessToken, filename);
			if (fileId == null)
			{
				await CreateNewFile(filename, fileContent, credentials);
			}
			else
			{
				await UpdateExistingFile(fileId, fileContent, credentials);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	private async Task CreateNewFile(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		string sessionUri = ((INameValueListBase<string>)(object)(await Flurl.Http.GeneratedExtensions.PostJsonAsync(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(UrlBuilderExtensions.SetQueryParam(GetFlurl().Request(new object[1] { "https://www.googleapis.com/upload/drive/v3/files" }), "uploadType", "resumable", false, (NullValueHandling)1), credentials.Token.AccessToken), "X-Upload-Content-Type", (object)"application/octet-stream"), "X-Upload-Content-Length", (object)fileContent.Length), (object)new
		{
			name = filename,
			parents = new string[1] { "appDataFolder" }
		}, (HttpCompletionOption)0, default(CancellationToken))).Headers).FirstOrDefault("Location");
		HttpContent content = (HttpContent)new ByteArrayContent(fileContent);
		await Flurl.Http.GeneratedExtensions.PostAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { sessionUri }), credentials.Token.AccessToken), content, (HttpCompletionOption)0, default(CancellationToken));
	}

	private async Task UpdateExistingFile(string fileId, byte[] fileContent, CloudStorageCredentials credentials)
	{
		string sessionUri = ((INameValueListBase<string>)(object)(await Flurl.Http.GeneratedExtensions.PatchAsync(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(UrlBuilderExtensions.SetQueryParam(GetFlurl().Request(new object[2] { "https://www.googleapis.com/upload/drive/v3/files", fileId }), "uploadType", "resumable", false, (NullValueHandling)1), credentials.Token.AccessToken), "X-Upload-Content-Type", (object)"application/octet-stream"), "X-Upload-Content-Length", (object)fileContent.Length), (HttpContent)null, (HttpCompletionOption)0, default(CancellationToken))).Headers).FirstOrDefault("Location");
		HttpContent content = (HttpContent)new ByteArrayContent(fileContent);
		await Flurl.Http.GeneratedExtensions.PutAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { sessionUri }), credentials.Token.AccessToken), content, (HttpCompletionOption)0, default(CancellationToken));
	}

	public override async Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			string fileId = await FindFileIdAsync(credentials.Token.AccessToken, filename);
			if (fileId == null)
			{
				throw new ConnectionFailedException($"The file '{filename}' does not exist.", null);
			}
			return await ResponseExtensions.ReceiveBytes(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[2] { "https://www.googleapis.com/drive/v3/files", fileId }), credentials.Token.AccessToken), "alt", "media", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken)));
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
			string fileId = await FindFileIdAsync(credentials.Token.AccessToken, filename);
			if (fileId == null)
			{
				throw new ConnectionFailedException($"The file '{filename}' does not exist.", null);
			}
			await Flurl.Http.GeneratedExtensions.DeleteAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[2] { "https://www.googleapis.com/drive/v3/files", fileId }), credentials.Token.AccessToken), (HttpCompletionOption)0, default(CancellationToken));
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
			JsonFolderEntries entries = null;
			do
			{
				entries = JsonSerializer.Deserialize<JsonFolderEntries>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://www.googleapis.com/drive/v3/files" }), credentials.Token.AccessToken), "spaces", "appDataFolder", false, (NullValueHandling)1), "pageToken", entries?.Cursor, false, (NullValueHandling)1), "fields", "nextPageToken, incompleteSearch, files(kind, id, name)", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
				result.AddRange(from item in entries.Entries
					where item.Kind == "drive#file"
					select item.Name);
			}
			while (entries?.Cursor != null);
			return result;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task<bool> ExistsFileAsync(string filename, CloudStorageCredentials credentials)
	{
		return !string.IsNullOrEmpty(await FindFileIdAsync(credentials.Token.AccessToken, filename));
	}

	private async Task<string> FindFileIdAsync(string accessToken, string filename)
	{
		try
		{
			JsonFolderEntries entries = JsonSerializer.Deserialize<JsonFolderEntries>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://www.googleapis.com/drive/v3/files" }), accessToken), "spaces", "appDataFolder", false, (NullValueHandling)1), "q", $"name='{filename}'", false, (NullValueHandling)1), "fields", "nextPageToken, incompleteSearch, files(kind, id, name)", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
			return (from item in entries.Entries
				where item.Kind == "drive#file"
				select item.Id).FirstOrDefault();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}
}

