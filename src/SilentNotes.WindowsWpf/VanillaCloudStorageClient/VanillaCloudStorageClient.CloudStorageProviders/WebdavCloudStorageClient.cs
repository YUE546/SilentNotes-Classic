using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Flurl;
using Flurl.Http;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class WebdavCloudStorageClient : CloudStorageClientBase, ICloudStorageClient
{
	private readonly bool _useSocketsForPropFind;

	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Username | CloudStorageCredentialsRequirements.Password | CloudStorageCredentialsRequirements.Url | CloudStorageCredentialsRequirements.AcceptUnsafeCertificate;

	public WebdavCloudStorageClient(bool useSocketsForPropFind)
	{
		_useSocketsForPropFind = useSocketsForPropFind;
	}

	public override async Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			HttpContent content = (HttpContent)new ByteArrayContent(fileContent);
			string urlWithSlash = CloudStorageClientBase.IncludeTrailingSlash(credentials.Url);
			await Flurl.Http.GeneratedExtensions.PutAsync(GetFlurl(credentials.AcceptInvalidCertificate).Request(new object[2] { urlWithSlash, filename }).WithBasicAuthOrAnonymous<IFlurlRequest>(credentials.Username, credentials.UnprotectedPassword), content, (HttpCompletionOption)0, default(CancellationToken));
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			string urlWithSlash = CloudStorageClientBase.IncludeTrailingSlash(credentials.Url);
			return await Flurl.Http.GeneratedExtensions.GetBytesAsync(GetFlurl(credentials.AcceptInvalidCertificate).Request(new object[2] { urlWithSlash, filename }).WithBasicAuthOrAnonymous<IFlurlRequest>(credentials.Username, credentials.UnprotectedPassword), (HttpCompletionOption)0, default(CancellationToken));
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task DeleteFileAsync(string filename, CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			string urlWithSlash = CloudStorageClientBase.IncludeTrailingSlash(credentials.Url);
			await Flurl.Http.GeneratedExtensions.DeleteAsync(GetFlurl(credentials.AcceptInvalidCertificate).Request(new object[2] { urlWithSlash, filename }).WithBasicAuthOrAnonymous<IFlurlRequest>(credentials.Username, credentials.UnprotectedPassword), (HttpCompletionOption)0, default(CancellationToken));
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public override async Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials)
	{
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			byte[] requestBytes = Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='utf-8'?>\r\n<D:propfind xmlns:D='DAV:'>\r\n<D:prop>\r\n    <D:resourcetype/>\r\n</D:prop>\r\n</D:propfind>");
			HttpContent content = (HttpContent)new ByteArrayContent(requestBytes);
			Url url = new Url(CloudStorageClientBase.IncludeTrailingSlash(credentials.Url));
			XDocument responseXml;
			if (!_useSocketsForPropFind)
			{
				using Stream responseStream = await ResponseExtensions.ReceiveStream(SettingsExtensions.WithTimeout<IFlurlRequest>(HeaderExtensions.WithHeader<IFlurlRequest>(HeaderExtensions.WithHeader<IFlurlRequest>(GetFlurl(credentials.AcceptInvalidCertificate).Request(new object[1] { url }).WithBasicAuthOrAnonymous<IFlurlRequest>(credentials.Username, credentials.UnprotectedPassword), "Depth", (object)"1"), "Content-Type", (object)"application/xml"), 20).SendAsync(new HttpMethod("PROPFIND"), content, (HttpCompletionOption)0, default(CancellationToken)));
				responseXml = XDocument.Load(responseStream);
			}
			else
			{
				HttpClientHandler httpMessageHandler = new HttpClientHandler
				{
					AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate),
					Credentials = new NetworkCredential(credentials.Username, credentials.Password)
				};
				try
				{
					httpMessageHandler.ServerCertificateCustomValidationCallback = (HttpRequestMessage sender, X509Certificate2 cert, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
					HttpClient httpClient = new HttpClient((HttpMessageHandler)(object)httpMessageHandler, false)
					{
						Timeout = TimeSpan.FromSeconds(20.0)
					};
					try
					{
						HttpRequestMessage msg = new HttpRequestMessage(new HttpMethod("PROPFIND"), (string)url);
						try
						{
							msg.Content = content;
							((HttpHeaders)msg.Headers).TryAddWithoutValidation("Depth", "1");
							msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
							HttpResponseMessage response = await httpClient.SendAsync(msg, (HttpCompletionOption)0);
							try
							{
								response.EnsureSuccessStatusCode();
								using Stream responseStream2 = await response.Content.ReadAsStreamAsync();
								responseXml = XDocument.Load(responseStream2);
							}
							finally
							{
								((IDisposable)response)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)msg)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)httpClient)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)httpMessageHandler)?.Dispose();
				}
			}
			return ParseWebdavResponseForFileNames(responseXml);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			throw CloudStorageClientBase.ConvertToCloudStorageException(ex2);
		}
	}

	public static List<string> ParseWebdavResponseForFileNames(XDocument responseXml)
	{
		List<string> list = new List<string>();
		IEnumerable<XElement> enumerable = from descendant in responseXml.Descendants()
			where string.Equals("response", descendant.Name.LocalName, StringComparison.OrdinalIgnoreCase)
			select descendant;
		foreach (XElement item in enumerable)
		{
			XElement element = (from descendant in item.Descendants()
				where string.Equals("resourcetype", descendant.Name.LocalName, StringComparison.OrdinalIgnoreCase)
				select descendant).FirstOrDefault();
			if (ExistsAndHasNoChilds(element))
			{
				XElement xElement = (from descendant in item.Elements()
					where string.Equals("href", descendant.Name.LocalName, StringComparison.OrdinalIgnoreCase)
					select descendant).FirstOrDefault();
				if (xElement != null)
				{
					string path = Url.Decode(xElement.Value, false);
					list.Add(Path.GetFileName(path));
				}
			}
		}
		return list;
	}

	private static bool ExistsAndHasNoChilds(XElement element)
	{
		return element != null && !element.HasElements;
	}
}


