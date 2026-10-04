using System;
using System.Threading.Tasks;
using SilentNotes.Services;
using VanillaCloudStorageClient;
using VanillaCloudStorageClient.CloudStorageProviders;

namespace SilentNotes.WindowsWpf.Services
{
    /// <summary>
    /// Simplified WebDAV upload helper with basic logging.
    /// </summary>
    internal class WebDavDiagnostics
    {
        private readonly ILogService _log;

        public WebDavDiagnostics(ILogService log)
        {
            _log = log;
        }

        /// <summary>
        /// Uploads a file to WebDAV with logging.
        /// </summary>
        public async Task UploadAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
        {
            _log.Info($"上传到 WebDAV: {filename} ({fileContent.Length} bytes)");

            var client = new WebdavCloudStorageClient(false);
            await client.UploadFileAsync(filename, fileContent, credentials);

            _log.Info("上传成功");
        }
    }
}
