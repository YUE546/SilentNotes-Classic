// Compat re-implementation of CloudStorageClientBase for .NET 4.0.
// The original (frozen) base class depends on Flurl.Http / FluentFTP and on
// HttpRequestException.StatusCode (.NET 5+). This version builds the exception mapping on
// net40's HttpWebRequest/WebException and keeps the same public/protected surface which the
// cloud storage clients and the Windows apps rely on.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VanillaCloudStorageClient
{
    /// <summary>
    /// Base class for implementations of the <see cref="ICloudStorageClient"/> interface.
    /// </summary>
    public abstract class CloudStorageClientBase : ICloudStorageClient
    {
        /// <inheritdoc/>
        public abstract CloudStorageCredentialsRequirements CredentialsRequirements { get; }

        /// <inheritdoc/>
        public abstract Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials);

        /// <inheritdoc/>
        public abstract Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials);

        /// <inheritdoc/>
        public abstract Task DeleteFileAsync(string filename, CloudStorageCredentials credentials);

        /// <inheritdoc/>
        public abstract Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials);

        /// <inheritdoc/>
        public virtual Task<bool> ExistsFileAsync(string filename, CloudStorageCredentials credentials)
        {
            return ListFileNamesAsync(credentials).ContinueWith(t =>
            {
                List<string> filenames = t.Result;
                return filenames.Contains(filename, StringComparer.InvariantCultureIgnoreCase);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>
        /// Ensures a trailing slash "/" at the end of the url, to mark a directory.
        /// </summary>
        /// <param name="url">Url with or without a trailing slash.</param>
        /// <returns>Returns the url with trailing slash, or the original url in case or null or empty.</returns>
        public static string IncludeTrailingSlash(string url)
        {
            if (!string.IsNullOrEmpty(url) && !url.EndsWith("/"))
            {
                return url + "/";
            }
            return url;
        }

        /// <summary>
        /// Analyses the exception and converts it to an exception deriving from
        /// <see cref="CloudStorageException"/>, so it can be re-thrown.
        /// </summary>
        /// <param name="catchedException">The original exception.</param>
        /// <returns>A cloud storage exception.</returns>
        protected static CloudStorageException ConvertToCloudStorageException(Exception catchedException)
        {
            if (catchedException is CloudStorageException catchedCloudStorageException)
            {
                // The catched exception is already of correct type.
                return catchedCloudStorageException;
            }
            else if (catchedException.InnerException is CloudStorageException innerCloudStorageException)
            {
                // The catched exception is already of correct type but is wrapped inside another exception.
                return innerCloudStorageException;
            }
            else if (catchedException is WebException webException)
            {
                switch (webException.Status)
                {
                    case WebExceptionStatus.Timeout:
                    case WebExceptionStatus.RequestCanceled:
                    case WebExceptionStatus.Pending:
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.ConnectionClosed:
                    case WebExceptionStatus.KeepAliveFailure:
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.ProxyNameResolutionFailure:
                        return new ConnectionFailedException("Timeout was reached", catchedException);
                }

                HttpWebResponse response = webException.Response as HttpWebResponse;
                if (response != null)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                        case HttpStatusCode.Forbidden:
                            return new AccessDeniedException(catchedException);
                        case HttpStatusCode.BadRequest:
                        case HttpStatusCode.NotFound:
                            return new ConnectionFailedException(catchedException);
                    }
                }
                return new ConnectionFailedException(catchedException);
            }
            else if (catchedException is UriFormatException)
            {
                return new CloudStorageException("The Url has an invalid format.", catchedException);
            }
            else if (catchedException is TaskCanceledException)
            {
                return new ConnectionFailedException("Timeout was reached", catchedException);
            }

            // Fallback to unexpected exception
            return new CloudStorageException("An unexpected error occured", catchedException);
        }
    }
}
