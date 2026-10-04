// Compat re-implementation of WebdavCloudStorageClient for .NET 4.0.
// The frozen original depends on Flurl.Http (net461+) and SocketsHttpHandler (.NET Core 2.0+).
// This version uses net40's native HttpWebRequest with the FromAsync pattern, keeping the
// public API identical (ctor(bool), all ICloudStorageClient members, and the static
// ParseWebdavResponseForFileNames used for testing). The "useSocketsForPropFind" argument is
// accepted for API compatibility but ignored: HttpWebRequest on Windows supports the custom
// "PROPFIND" method natively (the Android workaround does not apply here).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace VanillaCloudStorageClient.CloudStorageProviders
{
    /// <summary>
    /// Implementation of the <see cref="ICloudStorageClient"/> interface,
    /// which can handle cloud storage on WebDav servers.
    /// </summary>
    public class WebdavCloudStorageClient : CloudStorageClientBase, ICloudStorageClient
    {
        private const int DefaultTimeoutMs = 100000;
        private const int PropFindTimeoutMs = 20000;
        private const SecurityProtocolType SecurityProtocolTls12 = (SecurityProtocolType)3072;

        private readonly bool _useSocketsForPropFind;
        private static bool _tls12Configured;
        private static bool _unsafeCertificatesEnabled;

        /// <summary>
        /// Initializes a new intance of the <see cref="WebdavCloudStorageClient"/> class.
        /// </summary>
        /// <param name="useSocketsForPropFind">Kept for API compatibility with the frozen
        /// original, ignored on .NET 4.0.</param>
        public WebdavCloudStorageClient(bool useSocketsForPropFind)
        {
            _useSocketsForPropFind = useSocketsForPropFind;
        }

        /// <inheritdoc/>
        public override CloudStorageCredentialsRequirements CredentialsRequirements
        {
            get { return CloudStorageCredentialsRequirements.Username | CloudStorageCredentialsRequirements.Password | CloudStorageCredentialsRequirements.Url | CloudStorageCredentialsRequirements.AcceptUnsafeCertificate; }
        }

        /// <inheritdoc/>
        public override Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
        {
            return Task.Factory.StartNew(delegate
            {
                credentials.ThrowIfInvalid(CredentialsRequirements, true);
                try
                {
                    HttpWebRequest request = CreateRequest(CombineUrl(credentials.Url, filename), "PUT", credentials, credentials.AcceptInvalidCertificate, DefaultTimeoutMs);
                    request.ContentType = "application/octet-stream";
                    request.ContentLength = fileContent.Length;
                    using (Stream requestStream = GetRequestStream(request))
                    {
                        requestStream.Write(fileContent, 0, fileContent.Length);
                    }
                    CloseResponse(request);
                }
                catch (Exception ex)
                {
                    throw ConvertToCloudStorageException(ex);
                }
            });
        }

        /// <inheritdoc/>
        public override Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials)
        {
            return Task.Factory.StartNew(delegate
            {
                credentials.ThrowIfInvalid(CredentialsRequirements, true);
                try
                {
                    HttpWebRequest request = CreateRequest(CombineUrl(credentials.Url, filename), "GET", credentials, credentials.AcceptInvalidCertificate, DefaultTimeoutMs);
                    using (HttpWebResponse response = (HttpWebResponse)GetResponse(request))
                    using (Stream responseStream = response.GetResponseStream())
                    using (MemoryStream result = new MemoryStream())
                    {
                        responseStream.CopyTo(result);
                        return result.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw ConvertToCloudStorageException(ex);
                }
            });
        }

        /// <inheritdoc/>
        public override Task DeleteFileAsync(string filename, CloudStorageCredentials credentials)
        {
            return Task.Factory.StartNew(delegate
            {
                credentials.ThrowIfInvalid(CredentialsRequirements, true);
                try
                {
                    HttpWebRequest request = CreateRequest(CombineUrl(credentials.Url, filename), "DELETE", credentials, credentials.AcceptInvalidCertificate, DefaultTimeoutMs);
                    CloseResponse(request);
                }
                catch (Exception ex)
                {
                    throw ConvertToCloudStorageException(ex);
                }
            });
        }

        /// <inheritdoc/>
        public override Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials)
        {
            return Task.Factory.StartNew(delegate
            {
                credentials.ThrowIfInvalid(CredentialsRequirements, true);
                try
                {
                    // Request only resourcetype to reduce network traffic
                    byte[] requestBytes = Encoding.UTF8.GetBytes(
@"<?xml version='1.0' encoding='utf-8'?>
<D:propfind xmlns:D='DAV:'>
<D:prop>
    <D:resourcetype/>
</D:prop>
</D:propfind>");

                    HttpWebRequest request = CreateRequest(IncludeTrailingSlash(credentials.Url), "PROPFIND", credentials, credentials.AcceptInvalidCertificate, PropFindTimeoutMs);
                    request.Headers["Depth"] = "1";
                    request.ContentType = "application/xml";
                    request.ContentLength = requestBytes.Length;
                    using (Stream requestStream = GetRequestStream(request))
                    {
                        requestStream.Write(requestBytes, 0, requestBytes.Length);
                    }

                    XDocument responseXml;
                    using (HttpWebResponse response = (HttpWebResponse)GetResponse(request))
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        responseXml = XDocument.Load(responseStream);
                    }

                    // Files have an empty resourcetype element, folders a child element "collection"
                    return ParseWebdavResponseForFileNames(responseXml);
                }
                catch (Exception ex)
                {
                    throw ConvertToCloudStorageException(ex);
                }
            });
        }

        /// <summary>
        /// Made public for unit testing, this method interprets the response of a list file request.
        /// </summary>
        /// <param name="responseXml">Xml document generate from the webdav response.</param>
        /// <returns>List of file names.</returns>
        public static List<string> ParseWebdavResponseForFileNames(XDocument responseXml)
        {
            List<string> result = new List<string>();

            // Find all "response" elements, independend of their namespaces
            var responseElements = responseXml
                .Descendants()
                .Where(descendant => string.Equals("response", descendant.Name.LocalName, StringComparison.OrdinalIgnoreCase));

            foreach (XElement responseElement in responseElements)
            {
                // Find the "resourcetype" element, independend of its namespaces
                XElement resourceTypeElement = responseElement
                    .Descendants()
                    .Where(descendant => string.Equals("resourcetype", descendant.Name.LocalName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                // Files have an empty resourcetype
                bool isFile = ExistsAndHasNoChilds(resourceTypeElement);
                if (isFile)
                {
                    // Extract the "href" element, it contains the filename
                    XElement hrefElement = responseElement
                        .Elements()
                        .Where(descendant => string.Equals("href", descendant.Name.LocalName, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();

                    if (hrefElement != null)
                    {
                        string filePath = Uri.UnescapeDataString(hrefElement.Value);
                        result.Add(Path.GetFileName(filePath));
                    }
                }
            }
            return result;
        }

        private static HttpWebRequest CreateRequest(string url, string method, CloudStorageCredentials credentials, bool acceptInvalidCertificate, int timeoutMs)
        {
            ConfigureServicePoint(acceptInvalidCertificate);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            // "WithBasicAuthOrAnonymous" of the original: use basic auth if a password is
            // provided, otherwise send the request anonymously.
            if (!string.IsNullOrEmpty(credentials.UnprotectedPassword))
            {
                string authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.Username + ":" + credentials.UnprotectedPassword));
                request.Headers[HttpRequestHeader.Authorization] = "Basic " + authToken;
            }
            return request;
        }

        private static void ConfigureServicePoint(bool acceptInvalidCertificate)
        {
            // Many modern WebDAV servers require TLS 1.2, which .NET 4.0 does not enable by
            // default. Ignore errors of very old systems without TLS 1.2 support.
            if (!_tls12Configured)
            {
                try { ServicePointManager.SecurityProtocol |= SecurityProtocolTls12; }
                catch (NotSupportedException) { }
                _tls12Configured = true;
            }

            // .NET 4.0 knows only a global certificate validation callback (the per request
            // callback was introduced with .NET 4.5).
            if (acceptInvalidCertificate && !_unsafeCertificatesEnabled)
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                _unsafeCertificatesEnabled = true;
            }
        }

        private static string CombineUrl(string baseUrl, string filename)
        {
            return baseUrl.TrimEnd('/') + "/" + Uri.EscapeUriString(filename);
        }

        private static Stream GetRequestStream(WebRequest request)
        {
            return request.GetRequestStream();
        }

        private static WebResponse GetResponse(WebRequest request)
        {
            return request.GetResponse();
        }

        private static void CloseResponse(WebRequest request)
        {
            using (WebResponse response = GetResponse(request))
            {
            }
        }

        /// <summary>
        /// Checks whether the XElement is either an empty element (self closing), or doesn't have
        /// any child elements (opening and closing tag without children).
        /// </summary>
        /// <param name="element">Xml element to check.</param>
        /// <returns>True if it is an empty element, otherwise false.</returns>
        private static bool ExistsAndHasNoChilds(XElement element)
        {
            return (element != null) && !element.HasElements;
        }
    }
}
