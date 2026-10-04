using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using SilentNotes.Crypto;
using SilentNotes.Crypto.KeyDerivation;
using SilentNotes.Crypto.SymmetricEncryption;
using SilentNotes.Models;
using SilentNotes.Services;
using SilentNotes.Workers;
using VanillaCloudStorageClient;
using VanillaCloudStorageClient.CloudStorageProviders;
using DialogResult = System.Windows.Forms.DialogResult;

namespace SilentNotes.WindowsWinForms.Services
{
    internal class WindowsSynchronizationService
    {
        public static readonly string CloudFilename = "silentnotes_repository.silentnotes";
        private const string SyncBackupDir = "sync_backups";

        private readonly ISettingsService _settingsService;
        private readonly IRepositoryStorageService _repositoryStorageService;
        private readonly IDataProtectionService _dataProtectionService;
        private readonly ICryptoRandomService _cryptoRandomService;
        private readonly ILogService _log;
        private readonly IXmlFileService _xmlFileService;

        public WindowsSynchronizationService(
            ISettingsService settingsService,
            IRepositoryStorageService repositoryStorageService,
            IDataProtectionService dataProtectionService,
            ICryptoRandomService cryptoRandomService,
            ILogService log,
            IXmlFileService xmlFileService)
        {
            _settingsService = settingsService;
            _repositoryStorageService = repositoryStorageService;
            _dataProtectionService = dataProtectionService;
            _cryptoRandomService = cryptoRandomService;
            _log = log;
            _xmlFileService = xmlFileService;
        }

        public bool HasCloudStorageConfigured
        {
            get
            {
                var settings = _settingsService.LoadSettingsOrDefault();
                return settings.Credentials != null
                    && string.Equals(settings.Credentials.SerializeableCloudStorageId, "webdav", StringComparison.InvariantCultureIgnoreCase)
                    && !string.IsNullOrWhiteSpace(settings.Credentials.SerializeableUrl);
            }
        }

        public bool HasTransferCode
        {
            get { return _settingsService.LoadSettingsOrDefault().HasTransferCode; }
        }

        public CloudStorageCredentials GetCredentials()
        {
            var settings = _settingsService.LoadSettingsOrDefault();
            if (settings.Credentials == null)
                return null;

            string password = settings.Credentials.SerializeablePassword;
            if (!string.IsNullOrEmpty(password) && IsProbablyEncrypted(password))
            {
                try
                {
                    password = Encoding.UTF8.GetString(_dataProtectionService.Unprotect(password));
                }
                catch { }
            }

            return new CloudStorageCredentials
            {
                CloudStorageId = "webdav",
                Url = settings.Credentials.Url,
                Username = settings.Credentials.Username,
                UnprotectedPassword = password,
            };
        }

        private static bool IsProbablyEncrypted(string value)
        {
            return value.StartsWith("AQAAANCMnd8", StringComparison.Ordinal);
        }

        private string GetTransferCode()
        {
            return _settingsService.LoadSettingsOrDefault().TransferCode;
        }

        public void SetTransferCode(string transferCode)
        {
            var settings = _settingsService.LoadSettingsOrDefault();
            settings.TransferCode = transferCode?.Replace(" ", string.Empty);
            _settingsService.TrySaveSettingsToLocalDevice(settings);
        }

        private byte[] EncryptRepository(NoteRepositoryModel repository)
        {
            string transferCode = GetTransferCode();
            if (string.IsNullOrEmpty(transferCode))
                throw new InvalidOperationException("Transfer code not set.");

            byte[] binaryRepository = XmlUtils.SerializeToXmlBytes(repository);
            ICryptor encryptor = new Cryptor("SilentNotes", _cryptoRandomService);

            return encryptor.Encrypt(
                binaryRepository,
                CryptoUtils.StringToSecureString(transferCode),
                KeyDerivationCostType.Low,
                BouncyCastleAesGcm.CryptoAlgorithmName,
                Pbkdf2.CryptoKdfName,
                Cryptor.CompressionGzip);
        }

        private byte[] DecryptRepository(byte[] encryptedData)
        {
            string transferCode = GetTransferCode();
            if (string.IsNullOrEmpty(transferCode))
                throw new InvalidOperationException("Transfer code not set.");

            ICryptor decryptor = new Cryptor("SilentNotes", null);
            bool dummyNeedsReEncryption;
            return decryptor.Decrypt(encryptedData, CryptoUtils.StringToSecureString(transferCode), out dummyNeedsReEncryption);
        }

        private const int MaxBackupCount = 5;

        private void CreateLocalBackup()
        {
            try
            {
                string location = _repositoryStorageService.GetLocation();
                string repoPath = Path.Combine(location, NoteRepositoryModel.RepositoryFileName);
                if (!File.Exists(repoPath))
                    return;

                string backupDir = Path.Combine(location, SyncBackupDir);
                Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"presync_{timestamp}.silentnotes");
                File.Copy(repoPath, backupPath, overwrite: true);
                _log.Info($"已创建同步前备份: {backupPath}");

                CleanupOldBackups(backupDir);
            }
            catch (Exception)
            {
                _log.Info("创建同步前备份失败");
            }
        }

        private void CleanupOldBackups(string backupDir)
        {
            try
            {
                var backupFiles = Directory.GetFiles(backupDir, "presync_*.silentnotes")
                    .OrderBy(f => f)
                    .ToList();

                while (backupFiles.Count > MaxBackupCount)
                {
                    File.Delete(backupFiles[0]);
                    backupFiles.RemoveAt(0);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Compares two repositories using fingerprint to determine if they are equal.
        /// This matches the original SilentNotes behavior.
        /// </summary>
        private static bool RepositoriesAreEqual(NoteRepositoryModel repo1, NoteRepositoryModel repo2)
        {
            return repo1.GetModificationFingerprint() == repo2.GetModificationFingerprint();
        }



        public async Task<bool> UploadToCloudAsync(Action<string> progress = null)
        {
            try
            {
                var credentials = GetCredentials();
                if (credentials == null) { progress?.Invoke("未配置 WebDAV 凭据。"); return false; }

                progress?.Invoke("正在加载本地仓库...");
                NoteRepositoryModel localRepository;
                _repositoryStorageService.LoadRepositoryOrDefault(out localRepository);
                if (Object.ReferenceEquals(localRepository, NoteRepositoryModel.InvalidRepository))
                { progress?.Invoke("本地仓库无效。"); return false; }

                progress?.Invoke("正在加密仓库...");
                byte[] encrypted = EncryptRepository(localRepository);

                progress?.Invoke("正在上传到 WebDAV...");
                var diagnostics = new WebDavDiagnostics(_log);
                await diagnostics.UploadAsync(CloudFilename, encrypted, credentials);

                _log.Info("加密仓库上传成功");
                progress?.Invoke("上传成功！");
                return true;
            }
            catch (AccessDeniedException)
            {
                _log.Warning("上传失败：认证被拒");
                progress?.Invoke("上传失败：用户名或密码错误。");
                return false;
            }
            catch (Exception ex)
            {
                _log.Error("上传到 WebDAV 失败", ex);
                progress?.Invoke("上传失败：" + ex.Message);
                return false;
            }
        }

        public async Task<bool> DownloadFromCloudAsync(Action<string> progress = null)
        {
            try
            {
                var credentials = GetCredentials();
                if (credentials == null) { progress?.Invoke("未配置 WebDAV 凭据。"); return false; }

                progress?.Invoke("正在从 WebDAV 下载...");
                var client = new WebdavCloudStorageClient(false);
                byte[] encrypted;
                try { encrypted = await client.DownloadFileAsync(CloudFilename, credentials); }
                catch (Exception)
                {
                    _log.Info("云端无备份");
                    progress?.Invoke("云端没有找到备份文件。");
                    return false;
                }

                progress?.Invoke("正在解密仓库...");
                byte[] decrypted;
                try { decrypted = DecryptRepository(encrypted); }
                catch (CryptoDecryptionException)
                {
                    progress?.Invoke("解密失败：传输码不正确。");
                    _log.Warning("下载仓库解密失败（传输码错误）");
                    return false;
                }

                progress?.Invoke("正在解析仓库...");
                NoteRepositoryModel dummyLoadedRepo;
                if (!_repositoryStorageService.TryLoadRepositoryFromFile(decrypted, out dummyLoadedRepo))
                { progress?.Invoke("文件格式无效。"); return false; }

                progress?.Invoke("正在替换本地仓库...");
                try
                {
                    string location = _repositoryStorageService.GetLocation();
                    string xmlPath = Path.Combine(location, NoteRepositoryModel.RepositoryFileName);
                    File.WriteAllBytes(xmlPath, decrypted);
                    _repositoryStorageService.ClearCache();
                }
                catch (Exception ex)
                {
                    _log.Error("保存本地仓库失败", ex);
                    progress?.Invoke("保存失败：" + ex.Message);
                    return false;
                }

                progress?.Invoke("下载成功！请重新加载仓库。");
                return true;
            }
            catch (AccessDeniedException)
            {
                _log.Warning("下载失败：认证被拒");
                progress?.Invoke("下载失败：用户名或密码错误。");
                return false;
            }
            catch (Exception ex)
            {
                _log.Error("下载失败", ex);
                progress?.Invoke("下载失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Synchronizes local and cloud repositories using fingerprint-based comparison,
        /// matching the original SilentNotes behavior:
        /// 1. Compare fingerprints to detect changes
        /// 2. Use NoteRepositoryMerger for proper merging
        /// 3. Only upload/download if repositories differ
        /// </summary>
        public async Task<bool> SyncAsync(Action<string> progress = null)
        {
            try
            {
                var credentials = GetCredentials();
                if (credentials == null) { progress?.Invoke("未配置 WebDAV 凭据。"); return false; }

                NoteRepositoryModel localRepo;
                _repositoryStorageService.LoadRepositoryOrDefault(out localRepo);
                if (Object.ReferenceEquals(localRepo, NoteRepositoryModel.InvalidRepository))
                { progress?.Invoke("本地仓库无效。"); return false; }

                progress?.Invoke("正在检查云端...");
                var client = new WebdavCloudStorageClient(false);

                bool existsInCloud;
                try
                {
                    existsInCloud = await client.ExistsFileAsync(CloudFilename, credentials);
                }
                catch (AccessDeniedException) { throw; }
                catch (Exception)
                {
                    _log.Info("检查云端失败，假设不存在");
                    existsInCloud = false;
                }

                byte[] encrypted = null;

                if (!existsInCloud && !HasTransferCode)
                {
                    string autoCode = CryptoUtils.GenerateRandomBase62String(16, _cryptoRandomService);
                    SetTransferCode(autoCode);
                    _log.Info("已自动生成传输码（首次同步）");
                    progress?.Invoke("已自动生成传输码，请记住此码用于其他设备同步。");
                }
                else if (!HasTransferCode)
                {
                    progress?.Invoke("请先在设置中设置传输码。");
                    return false;
                }

                if (!existsInCloud)
                {
                    progress?.Invoke("云端无备份，正在上传...");
                    CreateLocalBackup();
                    return await UploadToCloudAsync(progress);
                }

                // Cloud file exists, now download it
                progress?.Invoke("正在下载云端仓库...");
                try { encrypted = await client.DownloadFileAsync(CloudFilename, credentials); }
                catch (AccessDeniedException) { throw; }
                catch (Exception ex)
                {
                    _log.Error("下载云端仓库失败", ex);
                    progress?.Invoke("下载失败：" + ex.Message);
                    return false;
                }

                byte[] decrypted;
                try { decrypted = DecryptRepository(encrypted); }
                catch (CryptoDecryptionException)
                {
                    progress?.Invoke("解析失败：传输码错误，请检查设置。");
                    return false;
                }

                NoteRepositoryModel cloudRepo;
                if (!_repositoryStorageService.TryLoadRepositoryFromFile(decrypted, out cloudRepo))
                {
                    progress?.Invoke("云端文件格式无效。");
                    return false;
                }

                // Check if repositories have the same ID (matching original SilentNotes behavior)
                if (localRepo.Id == cloudRepo.Id)
                {
                    // Same repository - automatic merge, no dialog
                    _log.Info($"同设备同步：本地 {localRepo.Notes.Count} 条，云端 {cloudRepo.Notes.Count} 条");
                    return await MergeAndSaveAsync(localRepo, cloudRepo, progress);
                }
                else
                {
                    // Different repositories - ask user what to do
                    _log.Info($"不同设备同步：本地 ID={localRepo.Id}，云端 ID={cloudRepo.Id}");
                    return await HandleDifferentRepositoryAsync(localRepo, cloudRepo, decrypted, progress);
                }
            }
            catch (AccessDeniedException)
            {
                _log.Warning("同步失败：认证被拒");
                progress?.Invoke("同步失败：用户名或密码错误。");
                return false;
            }
            catch (Exception ex)
            {
                _log.Error("同步失败", ex);
                progress?.Invoke("同步失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Merges local and cloud repositories and saves/uploads the result.
        /// Used when repositories have the same ID (same device).
        /// </summary>
        private async Task<bool> MergeAndSaveAsync(NoteRepositoryModel localRepo, NoteRepositoryModel cloudRepo, Action<string> progress)
        {
            NoteRepositoryMerger merger = new NoteRepositoryMerger();
            NoteRepositoryModel mergedRepo = merger.Merge(localRepo, cloudRepo);

            bool localChanged = !RepositoriesAreEqual(mergedRepo, localRepo);
            bool cloudChanged = !RepositoriesAreEqual(mergedRepo, cloudRepo);

            if (!localChanged && !cloudChanged)
            {
                progress?.Invoke("同步完成：无需更新。");
                return true;
            }

            CreateLocalBackup();

            if (localChanged)
            {
                _repositoryStorageService.TrySaveRepository(mergedRepo);
                _log.Info("已保存合并后的本地仓库");
            }

            if (cloudChanged)
            {
                progress?.Invoke("正在上传合并后的仓库到云端...");
                var credentials = GetCredentials();
                byte[] encryptedMerged = EncryptRepository(mergedRepo);
                var diagnostics = new WebDavDiagnostics(_log);
                await diagnostics.UploadAsync(CloudFilename, encryptedMerged, credentials);
                _log.Info("已上传合并后的仓库到云端");
            }

            progress?.Invoke("同步完成！");
            return true;
        }

        /// <summary>
        /// Shows the merge/use-cloud/cancel choice dialog, marshalled to the UI thread
        /// when called from a background timer.
        /// </summary>
        private static DialogResult ShowSyncChoiceDialog(int localCount, int cloudCount)
        {
            string message =
                "检测到不同设备的仓库（ID 不同），请选择操作：\n\n" +
                $"本地：{localCount} 条笔记\n" +
                $"云端：{cloudCount} 条笔记\n\n" +
                "点击「是」合并两个仓库（保留所有笔记）\n" +
                "点击「否」使用云端版本（覆盖本地）\n" +
                "点击「取消」取消同步";

            Func<DialogResult> show = delegate
            {
                return System.Windows.Forms.MessageBox.Show(
                    message,
                    "同步选择",
                    System.Windows.Forms.MessageBoxButtons.YesNoCancel,
                    System.Windows.Forms.MessageBoxIcon.Question);
            };

            try
            {
                if (System.Windows.Forms.Application.OpenForms.Count > 0)
                {
                    System.Windows.Forms.Form owner = System.Windows.Forms.Application.OpenForms[0];
                    if (owner != null && !owner.IsDisposed && owner.InvokeRequired)
                        return (DialogResult)owner.Invoke(show);
                }
            }
            catch
            {
            }
            return show();
        }

        /// <summary>
        /// Handles synchronization when local and cloud have different repository IDs.
        /// Shows a dialog asking user whether to merge, use local, or use cloud version.
        /// This matches the original SilentNotes ShowMergeChoiceStep behavior.
        /// </summary>
        private async Task<bool> HandleDifferentRepositoryAsync(NoteRepositoryModel localRepo, NoteRepositoryModel cloudRepo, byte[] decryptedCloudBytes, Action<string> progress)
        {
            int localCount = localRepo.Notes.Count(n => !n.InRecyclingBin);
            int cloudCount = cloudRepo.Notes.Count(n => !n.InRecyclingBin);

            DialogResult result = ShowSyncChoiceDialog(localCount, cloudCount);

            if (result == DialogResult.Cancel)
            {
                progress?.Invoke("同步已取消。");
                return false;
            }

            if (result == DialogResult.Yes)
            {
                // Merge both repositories
                progress?.Invoke("正在合并仓库...");
                return await MergeAndSaveAsync(localRepo, cloudRepo, progress);
            }

            if (result == DialogResult.No)
            {
                // Use cloud version
                progress?.Invoke("正在使用云端版本...");
                CreateLocalBackup();
                try
                {
                    string location = _repositoryStorageService.GetLocation();
                    string xmlPath = System.IO.Path.Combine(location, NoteRepositoryModel.RepositoryFileName);
                    System.IO.File.WriteAllBytes(xmlPath, decryptedCloudBytes);
                    _repositoryStorageService.ClearCache();
                    _log.Info("已使用云端版本覆盖本地");
                    progress?.Invoke("同步完成！");
                    return true;
                }
                catch (Exception ex)
                {
                    _log.Error("保存仓库失败", ex);
                    progress?.Invoke("保存失败：" + ex.Message);
                    return false;
                }
            }

            return false;
        }
    }
}
