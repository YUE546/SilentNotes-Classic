using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using VanillaCloudStorageClient.OAuth2;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class DropboxCloudStorageClient : OAuth2CloudStorageClient, ICloudStorageClient, IOAuth2CloudStorageClient
{
	private class JsonFolderEntries
	{
		[JsonPropertyName("entries")]
		public List<JsonFolderEntry> Entries { get; set; }

		[JsonPropertyName("cursor")]
		public string Cursor { get; set; }

		[JsonPropertyName("has_more")]
		public bool HasMore { get; set; }
	}

	private class JsonFolderEntry
	{
		[JsonPropertyName(".tag")]
		public string Tag { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("cursor")]
		public string Cursor { get; set; }

		[JsonPropertyName("has_more")]
		public bool HasMore { get; set; }
	}

	private const string AuthorizeUrl = "https://www.dropbox.com/oauth2/authorize";

	private const string TokenUrl = "https://api.dropbox.com/oauth2/token";

	private const string UploadUrl = "https://content.dropboxapi.com/2/files/upload";

	private const string DownloadUrl = "https://content.dropboxapi.com/2/files/download";

	private const string DeleteUrl = "https://api.dropboxapi.com/2/files/delete_v2";

	private const string ListUrl = "https://api.dropboxapi.com/2/files/list_folder";

	private const string ListContinueUrl = "https://api.dropboxapi.com/2/files/list_folder/continue";

	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Token;

	public DropboxCloudStorageClient(string oauthClientId, string oauthRedirectUrl)
		: base(new OAuth2Config
		{
			AuthorizeServiceEndpoint = "https://www.dropbox.com/oauth2/authorize",
			TokenServiceEndpoint = "https://api.dropbox.com/oauth2/token",
			ClientId = oauthClientId,
			RedirectUrl = oauthRedirectUrl,
			Flow = AuthorizationFlow.Code,
			Scope = null,
			ClientSecretHandling = ClientSecretHandling.DoNotSend
		})
	{
	}

	public override string BuildAuthorizationRequestUrl(string state, string codeVerifier)
	{
		string text = base.BuildAuthorizationRequestUrl(state, codeVerifier);
		return text + "&token_access_type=offline";
	}

	public override async Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			string jsonPathParameter = JsonSerializer.Serialize(new
			{
				path = EnsureLeadingSlash(filename),
				mode = "overwrite",
				autorename = false
			}, (JsonSerializerOptions)null);
			HttpContent content = (HttpContent)new ByteArrayContent(fileContent);
			await Flurl.Http.GeneratedExtensions.PostAsync(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://content.dropboxapi.com/2/files/upload" }), credentials.Token.AccessToken), "Dropbox-API-Arg", (object)jsonPathParameter), "Content-Type", (object)"application/octet-stream"), content, (HttpCompletionOption)0, default(CancellationToken));
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
			string jsonPathParameter = JsonSerializer.Serialize(new
			{
				path = EnsureLeadingSlash(filename)
			}, (JsonSerializerOptions)null);
			return await ResponseExtensions.ReceiveBytes(Flurl.Http.GeneratedExtensions.GetAsync(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://content.dropboxapi.com/2/files/download" }), credentials.Token.AccessToken), "Dropbox-API-Arg", (object)jsonPathParameter), (HttpCompletionOption)0, default(CancellationToken)));
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
			await Flurl.Http.GeneratedExtensions.PostJsonAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://api.dropboxapi.com/2/files/delete_v2" }), credentials.Token.AccessToken), (object)new
			{
				path = EnsureLeadingSlash(filename)
			}, (HttpCompletionOption)0, default(CancellationToken));
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
			JsonFolderEntries entries = JsonSerializer.Deserialize<JsonFolderEntries>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PostJsonAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://api.dropboxapi.com/2/files/list_folder" }), credentials.Token.AccessToken), (object)new
			{
				path = string.Empty,
				recursive = false,
				include_deleted = false,
				include_has_explicit_shared_members = false,
				include_mounted_folders = true,
				include_non_downloadable_files = false
			}, (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
			List<string> result = new List<string>();
			result.AddRange(from item in entries.Entries
				where item.Tag == "file"
				select item.Name);
			while (entries.HasMore)
			{
				entries = JsonSerializer.Deserialize<JsonFolderEntries>(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PostJsonAsync(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { "https://api.dropboxapi.com/2/files/list_folder/continue" }), credentials.Token.AccessToken), (object)new
				{
					cursor = entries.Cursor
				}, (HttpCompletionOption)0, default(CancellationToken))), (JsonSerializerOptions)null);
				result.AddRange(from item in entries.Entries
					where item.Tag == "file"
					select item.Name);
			}
			return result;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	private string EnsureLeadingSlash(string filename)
	{
		if (filename.StartsWith("/"))
		{
			return filename;
		}
		return "/" + filename;
	}
}

