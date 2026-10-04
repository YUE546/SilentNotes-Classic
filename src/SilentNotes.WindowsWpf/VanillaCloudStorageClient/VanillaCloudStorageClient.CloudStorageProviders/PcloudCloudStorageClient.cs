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
using VanillaCloudStorageClient.OAuth2;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class PcloudCloudStorageClient : OAuth2CloudStorageClient, ICloudStorageClient, IOAuth2CloudStorageClient
{
	public enum DataCenter
	{
		Us,
		Europe
	}

	private class JsonListFolder
	{
		[JsonPropertyName("metadata")]
		public JsonMetaData MetaData { get; set; }
	}

	private class JsonMetaData
	{
		[JsonPropertyName("contents")]
		public List<JsonMetaDataContent> Contents { get; set; }
	}

	private class JsonMetaDataContent
	{
		[JsonPropertyName("isfolder")]
		public bool IsFolder { get; set; }

		[JsonPropertyName("folderid")]
		public long? FolderId { get; set; }

		[JsonPropertyName("fileid")]
		public long? FileId { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("path")]
		public string Path { get; set; }
	}

	private class JsonFileLink
	{
		[JsonPropertyName("path")]
		public string Path { get; set; }

		[JsonPropertyName("hosts")]
		public List<string> Hosts { get; set; }
	}

	private class JsonError
	{
		[JsonPropertyName("result")]
		public long Result { get; set; }

		[JsonPropertyName("error")]
		public string Error { get; set; }
	}

	public class PcloudApiException : Exception
	{
		public long Result { get; }

		public string Error { get; }

		public PcloudApiException(long result, string error)
		{
			Result = result;
			Error = error;
		}
	}

	private const string AuthorizeUrl = "https://my.pcloud.com/oauth2/authorize";

	private const string TokenUrl = "https://{0}/oauth2_token";

	private const string UploadUrl = "https://{0}/uploadfile";

	private const string FileLinkUrl = "https://{0}/getfilelink";

	private const string DeleteUrl = "https://{0}/deletefile";

	private const string ListUrl = "https://{0}/listfolder";

	private const long APP_ROOT_FOLDER_ID = 0L;

	private readonly string _dataCenterHost;

	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Token;

	public PcloudCloudStorageClient(string oauthClientId, string oauthRedirectUrl, DataCenter dataCenter)
		: base(new OAuth2Config
		{
			AuthorizeServiceEndpoint = "https://my.pcloud.com/oauth2/authorize",
			TokenServiceEndpoint = $"https://{GetDataCenterHost(dataCenter)}/oauth2_token",
			ClientId = oauthClientId,
			RedirectUrl = oauthRedirectUrl,
			Flow = AuthorizationFlow.Token,
			ClientSecretHandling = ClientSecretHandling.DoNotSend
		})
	{
		_dataCenterHost = GetDataCenterHost(dataCenter);
	}

	public override async Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements);
		try
		{
			HttpContent content = (HttpContent)new ByteArrayContent(fileContent);
			string requestUrl = $"https://{_dataCenterHost}/uploadfile";
			ThrowIfErrorResponse(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PutAsync(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { requestUrl }), credentials.Token.AccessToken), "folderid", (object)0L, (NullValueHandling)1), "filename", filename, false, (NullValueHandling)1), "nopartial", (object)1, (NullValueHandling)1), "filtermeta", "fileid", false, (NullValueHandling)1), content, (HttpCompletionOption)0, default(CancellationToken))));
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
			long? fileId = await FindFileIdAsync(credentials.Token.AccessToken, filename);
			if (!fileId.HasValue)
			{
				throw new ConnectionFailedException($"The file '{filename}' does not exist.", null);
			}
			string requestUrl = $"https://{_dataCenterHost}/getfilelink";
			string jsonResponse = await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { requestUrl }), credentials.Token.AccessToken), "fileid", (object)fileId, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken)));
			ThrowIfErrorResponse(jsonResponse);
			JsonFileLink fileLink = JsonSerializer.Deserialize<JsonFileLink>(jsonResponse, (JsonSerializerOptions)null);
			requestUrl = $"https://{fileLink.Hosts[0]}{fileLink.Path}";
			return await ResponseExtensions.ReceiveBytes(Flurl.Http.GeneratedExtensions.GetAsync(GetFlurl().Request(new object[1] { requestUrl }), (HttpCompletionOption)0, default(CancellationToken)));
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
			long? fileId = await FindFileIdAsync(credentials.Token.AccessToken, filename);
			if (!fileId.HasValue)
			{
				throw new ConnectionFailedException($"The file '{filename}' does not exist.", null);
			}
			string requestUrl = $"https://{_dataCenterHost}/deletefile";
			ThrowIfErrorResponse(await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.PostAsync(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { requestUrl }), credentials.Token.AccessToken), "fileid", (object)fileId, (NullValueHandling)1), "filtermeta", "isdeleted", false, (NullValueHandling)1), (HttpContent)null, (HttpCompletionOption)0, default(CancellationToken))));
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
			string requestUrl = $"https://{_dataCenterHost}/listfolder";
			string jsonResponse = await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { requestUrl }), credentials.Token.AccessToken), "folderid", (object)0L, (NullValueHandling)1), "filtermeta", "name,isfolder", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken)));
			ThrowIfErrorResponse(jsonResponse);
			JsonListFolder entries = JsonSerializer.Deserialize<JsonListFolder>(jsonResponse, (JsonSerializerOptions)null);
			return (from item in entries.MetaData.Contents
				where !item.IsFolder
				select item.Name).ToList();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task<bool> ExistsFileAsync(string filename, CloudStorageCredentials credentials)
	{
		return (await FindFileIdAsync(credentials.Token.AccessToken, filename)).HasValue;
	}

	private async Task<long?> FindFileIdAsync(string accessToken, string filename)
	{
		try
		{
			string requestUrl = $"https://{_dataCenterHost}/listfolder";
			string jsonResponse = await ResponseExtensions.ReceiveString(Flurl.Http.GeneratedExtensions.GetAsync(UrlBuilderExtensions.SetQueryParam(UrlBuilderExtensions.SetQueryParam(HeaderExtensions.WithOAuthBearerToken<IFlurlRequest>(GetFlurl().Request(new object[1] { requestUrl }), accessToken), "folderid", (object)0L, (NullValueHandling)1), "filtermeta", "name,isfolder,fileid", false, (NullValueHandling)1), (HttpCompletionOption)0, default(CancellationToken)));
			ThrowIfErrorResponse(jsonResponse);
			JsonListFolder entries = JsonSerializer.Deserialize<JsonListFolder>(jsonResponse, (JsonSerializerOptions)null);
			return (from item in entries.MetaData.Contents
				where !item.IsFolder && string.Equals(filename, item.Name, StringComparison.InvariantCultureIgnoreCase)
				select item.FileId).FirstOrDefault();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	private static string GetDataCenterHost(DataCenter dataCenter)
	{
		return dataCenter switch
		{
			DataCenter.Us => "api.pcloud.com", 
			DataCenter.Europe => "eapi.pcloud.com", 
			_ => throw new InvalidParameterException("dataCenter"), 
		};
	}

	private static int GetDataCenterId(DataCenter dataCenter)
	{
		return dataCenter switch
		{
			DataCenter.Us => 1, 
			DataCenter.Europe => 2, 
			_ => throw new InvalidParameterException("dataCenter"), 
		};
	}

	private static void ThrowIfErrorResponse(string jsonResponse)
	{
		Exception ex = ResponseToException(jsonResponse);
		if (ex != null)
		{
			throw ex;
		}
	}

	private static Exception ResponseToException(string jsonResponse)
	{
		try
		{
			JsonError jsonError = JsonSerializer.Deserialize<JsonError>(jsonResponse, (JsonSerializerOptions)null);
			if (jsonError.Result != 0L && (jsonError.Result < 6000 || jsonError.Result >= 7000))
			{
				PcloudApiException ex = new PcloudApiException(jsonError.Result, jsonError.Error);
				switch (ex.Result)
				{
				case 2011L:
				case 2041L:
				case 5001L:
				case 5002L:
					return new ConnectionFailedException(ex);
				case 1000L:
				case 2000L:
				case 2003L:
				case 2094L:
				case 4000L:
					return new AccessDeniedException(ex);
				default:
					return new CloudStorageException("Error", ex);
				}
			}
		}
		catch (Exception)
		{
			return null;
		}
		return null;
	}
}

