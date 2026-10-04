using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentFTP.Exceptions;
using Flurl.Http;

namespace VanillaCloudStorageClient;

public abstract class CloudStorageClientBase : ICloudStorageClient
{
	private static IFlurlClient _sharedFlurlClient;

	private static HttpClientHandler _sharedHttpClientHandler;

	public abstract CloudStorageCredentialsRequirements CredentialsRequirements { get; }

	public abstract Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials);

	public abstract Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials);

	public abstract Task DeleteFileAsync(string filename, CloudStorageCredentials credentials);

	public abstract Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials);

	public virtual async Task<bool> ExistsFileAsync(string filename, CloudStorageCredentials credentials)
	{
		return (await ListFileNamesAsync(credentials)).Contains(filename, StringComparer.InvariantCultureIgnoreCase);
	}

	protected IFlurlClient GetFlurl(bool acceptUnsafeCertificates = false)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Expected O, but got Unknown
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		bool flag = _sharedFlurlClient != null;
		int num;
		if (flag)
		{
			HttpClientHandler sharedHttpClientHandler = _sharedHttpClientHandler;
			num = ((((sharedHttpClientHandler != null) ? sharedHttpClientHandler.ServerCertificateCustomValidationCallback : null) != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag2 = (byte)num != 0;
		if (flag && acceptUnsafeCertificates != flag2)
		{
			_sharedHttpClientHandler = null;
			((IDisposable)_sharedFlurlClient).Dispose();
			_sharedFlurlClient = null;
		}
		if (_sharedFlurlClient == null)
		{
			if (acceptUnsafeCertificates)
			{
				_sharedHttpClientHandler = new HttpClientHandler
				{
					ServerCertificateCustomValidationCallback = (HttpRequestMessage sender, X509Certificate2 cert, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true
				};
				_sharedFlurlClient = (IFlurlClient)new FlurlClient(new HttpClient((HttpMessageHandler)(object)_sharedHttpClientHandler), (string)null);
			}
			else
			{
				HttpClientHandler val = new HttpClientHandler();
				_sharedFlurlClient = (IFlurlClient)new FlurlClient(new HttpClient((HttpMessageHandler)(object)val), (string)null);
			}
		}
		return _sharedFlurlClient;
	}

	public static string IncludeTrailingSlash(string url)
	{
		if (!string.IsNullOrEmpty(url) && !url.EndsWith("/"))
		{
			return url + "/";
		}
		return url;
	}

	protected static CloudStorageException ConvertToCloudStorageException(Exception catchedException)
	{
		if (catchedException is CloudStorageException result)
		{
			return result;
		}
		if (catchedException.InnerException is CloudStorageException result2)
		{
			return result2;
		}
		FlurlHttpException flurlHttpException;
		if ((flurlHttpException = (FlurlHttpException)(object)((catchedException is FlurlHttpException) ? catchedException : null)) != null)
		{
			if (catchedException is FlurlHttpTimeoutException)
			{
				return new ConnectionFailedException("Timeout was reached", catchedException);
			}
			if (catchedException is FlurlParsingException)
			{
				return new CloudStorageException("The web response had an unexpected format", catchedException);
			}
			switch (flurlHttpException.GetHttpStatusCode())
			{
			case HttpStatusCode.Unauthorized:
			case HttpStatusCode.Forbidden:
				return new AccessDeniedException(catchedException);
			case HttpStatusCode.BadRequest:
			case HttpStatusCode.NotFound:
				return new ConnectionFailedException(catchedException);
			}
		}
		else
		{
			if (catchedException is FtpException)
			{
				if (catchedException is FtpAuthenticationException)
				{
					return new AccessDeniedException(catchedException);
				}
				FtpCommandException ex;
				if ((ex = (FtpCommandException)(object)((catchedException is FtpCommandException) ? catchedException : null)) != null && ex.CompletionCode.StartsWith("53"))
				{
					return new AccessDeniedException(catchedException);
				}
				return new ConnectionFailedException(catchedException);
			}
			HttpRequestException innerException;
			if ((innerException = (HttpRequestException)(object)((catchedException is HttpRequestException) ? catchedException : null)) != null)
			{
				return new ConnectionFailedException((Exception)(object)innerException);
			}
			if (catchedException is UriFormatException)
			{
				return new CloudStorageException("The Url has an invalid format.", catchedException);
			}
			if (catchedException is TaskCanceledException)
			{
				return new ConnectionFailedException("Timeout was reached", catchedException);
			}
		}
		return new CloudStorageException("An unexpected error occured", catchedException);
	}
}
