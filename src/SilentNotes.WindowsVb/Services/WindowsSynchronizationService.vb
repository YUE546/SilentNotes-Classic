Option Strict On
Option Explicit On
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualBasic
Imports DialogResult = System.Windows.Forms.DialogResult
Imports SilentNotes.Crypto
Imports SilentNotes.Crypto.KeyDerivation
Imports SilentNotes.Crypto.SymmetricEncryption
Imports SilentNotes.Models
Imports SilentNotes.Services
Imports SilentNotes.Workers
Imports VanillaCloudStorageClient
Imports VanillaCloudStorageClient.CloudStorageProviders

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>
    ''' WebDAV 同步服务（VB10 重写版）：C# 原版是 async/await 链，VB10 无 async——
    ''' 全链路改为同步方法，由调用方（MainForm）在后台线程上执行；底层 WebDAV 客户端
    ''' 保留 Task 签名（与 C# 版共用同一 DLL），通过 TaskUtils.WaitAndUnwrap 等待并还原异常。
    ''' 进度与选择框通过既有机制 marshal 回 UI 线程。
    ''' </summary>
    Friend Class WindowsSynchronizationService
        Public Shared ReadOnly CloudFilename As String = "silentnotes_repository.silentnotes"
        Private Const SyncBackupDir As String = "sync_backups"

        Private ReadOnly _settingsService As ISettingsService
        Private ReadOnly _repositoryStorageService As IRepositoryStorageService
        Private ReadOnly _dataProtectionService As IDataProtectionService
        Private ReadOnly _cryptoRandomService As ICryptoRandomService
        Private ReadOnly _log As ILogService
        Private ReadOnly _xmlFileService As IXmlFileService

        Public Sub New(
            settingsService As ISettingsService,
            repositoryStorageService As IRepositoryStorageService,
            dataProtectionService As IDataProtectionService,
            cryptoRandomService As ICryptoRandomService,
            log As ILogService,
            xmlFileService As IXmlFileService)
            _settingsService = settingsService
            _repositoryStorageService = repositoryStorageService
            _dataProtectionService = dataProtectionService
            _cryptoRandomService = cryptoRandomService
            _log = log
            _xmlFileService = xmlFileService
        End Sub

        Public ReadOnly Property HasCloudStorageConfigured As Boolean
            Get
                Dim settings As SettingsModel = _settingsService.LoadSettingsOrDefault()
                Return settings.Credentials IsNot Nothing _
                    AndAlso String.Equals(settings.Credentials.SerializeableCloudStorageId, "webdav", StringComparison.InvariantCultureIgnoreCase) _
                    AndAlso Not String.IsNullOrWhiteSpace(settings.Credentials.SerializeableUrl)
            End Get
        End Property

        Public ReadOnly Property HasTransferCode As Boolean
            Get
                Return _settingsService.LoadSettingsOrDefault().HasTransferCode
            End Get
        End Property

        Public Function GetCredentials() As CloudStorageCredentials
            Dim settings As SettingsModel = _settingsService.LoadSettingsOrDefault()
            If settings.Credentials Is Nothing Then
                Return Nothing
            End If

            Dim password As String = settings.Credentials.SerializeablePassword
            If Not String.IsNullOrEmpty(password) AndAlso IsProbablyEncrypted(password) Then
                Try
                    password = Encoding.UTF8.GetString(_dataProtectionService.Unprotect(password))
                Catch
                End Try
            End If

            Return New CloudStorageCredentials With {
                .CloudStorageId = "webdav",
                .Url = settings.Credentials.Url,
                .Username = settings.Credentials.Username,
                .UnprotectedPassword = password
            }
        End Function

        Private Shared Function IsProbablyEncrypted(value As String) As Boolean
            Return value.StartsWith("AQAAANCMnd8", StringComparison.Ordinal)
        End Function

        Private Function GetTransferCode() As String
            Return _settingsService.LoadSettingsOrDefault().TransferCode
        End Function

        Public Sub SetTransferCode(transferCode As String)
            Dim settings As SettingsModel = _settingsService.LoadSettingsOrDefault()
            Dim code As String = transferCode
            If code IsNot Nothing Then
                code = code.Replace(" ", String.Empty)
            End If
            settings.TransferCode = code
            _settingsService.TrySaveSettingsToLocalDevice(settings)
        End Sub

        Private Function EncryptRepository(repository As NoteRepositoryModel) As Byte()
            Dim transferCode As String = GetTransferCode()
            If String.IsNullOrEmpty(transferCode) Then
                Throw New InvalidOperationException("Transfer code not set.")
            End If

            Dim binaryRepository As Byte() = XmlUtils.SerializeToXmlBytes(repository)
            Dim encryptor As ICryptor = New Cryptor("SilentNotes", _cryptoRandomService)

            Return encryptor.Encrypt(
                binaryRepository,
                CryptoUtils.StringToSecureString(transferCode),
                KeyDerivationCostType.Low,
                BouncyCastleAesGcm.CryptoAlgorithmName,
                Pbkdf2.CryptoKdfName,
                Cryptor.CompressionGzip)
        End Function

        Private Function DecryptRepository(encryptedData As Byte()) As Byte()
            Dim transferCode As String = GetTransferCode()
            If String.IsNullOrEmpty(transferCode) Then
                Throw New InvalidOperationException("Transfer code not set.")
            End If

            Dim decryptor As ICryptor = New Cryptor("SilentNotes", Nothing)
            Dim dummyNeedsReEncryption As Boolean = False
            Return decryptor.Decrypt(encryptedData, CryptoUtils.StringToSecureString(transferCode), dummyNeedsReEncryption)
        End Function

        Private Const MaxBackupCount As Integer = 5

        Private Sub CreateLocalBackup()
            Try
                Dim location As String = _repositoryStorageService.GetLocation()
                Dim repoPath As String = Path.Combine(location, NoteRepositoryModel.RepositoryFileName)
                If Not File.Exists(repoPath) Then
                    Return
                End If

                Dim backupDir As String = Path.Combine(location, SyncBackupDir)
                Directory.CreateDirectory(backupDir)

                Dim backupPath As String = Path.Combine(backupDir,
                    String.Format("presync_{0:yyyyMMdd_HHmmss}.silentnotes", DateTime.Now))
                File.Copy(repoPath, backupPath, True)
                _log.Info(String.Format("已创建同步前备份: {0}", backupPath))

                CleanupOldBackups(backupDir)
            Catch
                _log.Info("创建同步前备份失败")
            End Try
        End Sub

        Private Sub CleanupOldBackups(backupDir As String)
            Try
                Dim backupFiles As List(Of String) = Directory.GetFiles(backupDir, "presync_*.silentnotes").
                    OrderBy(Function(f) f).
                    ToList()

                While backupFiles.Count > MaxBackupCount
                    File.Delete(backupFiles(0))
                    backupFiles.RemoveAt(0)
                End While
            Catch
            End Try
        End Sub

        ''' <summary>用指纹比较两个仓库是否一致，与原版 SilentNotes 行为匹配。</summary>
        Private Shared Function RepositoriesAreEqual(repo1 As NoteRepositoryModel, repo2 As NoteRepositoryModel) As Boolean
            Return repo1.GetModificationFingerprint() = repo2.GetModificationFingerprint()
        End Function

        ''' <summary>progress?.Invoke 的 VB10 等价写法。</summary>
        Private Shared Sub Report(progress As Action(Of String), message As String)
            If progress IsNot Nothing Then
                progress(message)
            End If
        End Sub

        Public Function UploadToCloud(Optional progress As Action(Of String) = Nothing) As Boolean
            Try
                Dim credentials As CloudStorageCredentials = GetCredentials()
                If credentials Is Nothing Then
                    Report(progress, "未配置 WebDAV 凭据。")
                    Return False
                End If

                Report(progress, "正在加载本地仓库...")
                Dim localRepository As NoteRepositoryModel = Nothing
                _repositoryStorageService.LoadRepositoryOrDefault(localRepository)
                If Object.ReferenceEquals(localRepository, NoteRepositoryModel.InvalidRepository) Then
                    Report(progress, "本地仓库无效。")
                    Return False
                End If

                Report(progress, "正在加密仓库...")
                Dim encrypted As Byte() = EncryptRepository(localRepository)

                Report(progress, "正在上传到 WebDAV...")
                Dim diagnostics As New WebDavDiagnostics(_log)
                diagnostics.Upload(CloudFilename, encrypted, credentials)

                _log.Info("加密仓库上传成功")
                Report(progress, "上传成功！")
                Return True
            Catch ex As AccessDeniedException
                _log.Warning("上传失败：认证被拒")
                Report(progress, "上传失败：用户名或密码错误。")
                Return False
            Catch ex As Exception
                _log.[Error]("上传到 WebDAV 失败", ex)
                Report(progress, "上传失败：" & ex.Message)
                Return False
            End Try
        End Function

        ' C# 版里无任何调用者（死代码），为保持行为一致仍保留。
        Public Function DownloadFromCloud(Optional progress As Action(Of String) = Nothing) As Boolean
            Try
                Dim credentials As CloudStorageCredentials = GetCredentials()
                If credentials Is Nothing Then
                    Report(progress, "未配置 WebDAV 凭据。")
                    Return False
                End If

                Report(progress, "正在从 WebDAV 下载...")
                Dim client As New WebdavCloudStorageClient(False)
                Dim encrypted As Byte()
                Try
                    encrypted = TaskUtils.WaitAndUnwrap(Of Byte())(client.DownloadFileAsync(CloudFilename, credentials))
                Catch
                    _log.Info("云端无备份")
                    Report(progress, "云端没有找到备份文件。")
                    Return False
                End Try

                Report(progress, "正在解密仓库...")
                Dim decrypted As Byte()
                Try
                    decrypted = DecryptRepository(encrypted)
                Catch ex As CryptoDecryptionException
                    Report(progress, "解密失败：传输码不正确。")
                    _log.Warning("下载仓库解密失败（传输码错误）")
                    Return False
                End Try

                Report(progress, "正在解析仓库...")
                Dim dummyLoadedRepo As NoteRepositoryModel = Nothing
                If Not _repositoryStorageService.TryLoadRepositoryFromFile(decrypted, dummyLoadedRepo) Then
                    Report(progress, "文件格式无效。")
                    Return False
                End If

                Report(progress, "正在替换本地仓库...")
                Try
                    Dim location As String = _repositoryStorageService.GetLocation()
                    Dim xmlPath As String = Path.Combine(location, NoteRepositoryModel.RepositoryFileName)
                    File.WriteAllBytes(xmlPath, decrypted)
                    _repositoryStorageService.ClearCache()
                Catch ex As Exception
                    _log.[Error]("保存本地仓库失败", ex)
                    Report(progress, "保存失败：" & ex.Message)
                    Return False
                End Try

                Report(progress, "下载成功！请重新加载仓库。")
                Return True
            Catch ex As AccessDeniedException
                _log.Warning("下载失败：认证被拒")
                Report(progress, "下载失败：用户名或密码错误。")
                Return False
            Catch ex As Exception
                _log.[Error]("下载失败", ex)
                Report(progress, "下载失败：" & ex.Message)
                Return False
            End Try
        End Function

        ''' <summary>
        ''' 基于指纹比较同步本地与云端仓库，与原版 SilentNotes 行为匹配：
        ''' 1. 比较指纹检测变化；2. 用 NoteRepositoryMerger 正确合并；3. 仅在仓库不同时上传/下载。
        ''' 必须在后台线程调用（网络 + PBKDF2 是阻塞操作）。
        ''' </summary>
        Public Function Sync(Optional progress As Action(Of String) = Nothing) As Boolean
            Try
                Dim credentials As CloudStorageCredentials = GetCredentials()
                If credentials Is Nothing Then
                    Report(progress, "未配置 WebDAV 凭据。")
                    Return False
                End If

                Dim localRepo As NoteRepositoryModel = Nothing
                _repositoryStorageService.LoadRepositoryOrDefault(localRepo)
                If Object.ReferenceEquals(localRepo, NoteRepositoryModel.InvalidRepository) Then
                    Report(progress, "本地仓库无效。")
                    Return False
                End If

                Report(progress, "正在检查云端...")
                Dim client As New WebdavCloudStorageClient(False)

                Dim existsInCloud As Boolean
                Try
                    existsInCloud = TaskUtils.WaitAndUnwrap(Of Boolean)(client.ExistsFileAsync(CloudFilename, credentials))
                Catch ex As AccessDeniedException
                    Throw
                Catch
                    _log.Info("检查云端失败，假设不存在")
                    existsInCloud = False
                End Try

                If Not existsInCloud AndAlso Not HasTransferCode Then
                    Dim autoCode As String = CryptoUtils.GenerateRandomBase62String(16, _cryptoRandomService)
                    SetTransferCode(autoCode)
                    _log.Info("已自动生成传输码（首次同步）")
                    Report(progress, "已自动生成传输码，请记住此码用于其他设备同步。")
                ElseIf Not HasTransferCode Then
                    Report(progress, "请先在设置中设置传输码。")
                    Return False
                End If

                If Not existsInCloud Then
                    Report(progress, "云端无备份，正在上传...")
                    CreateLocalBackup()
                    Return UploadToCloud(progress)
                End If

                ' 云端文件存在，下载它
                Report(progress, "正在下载云端仓库...")
                Dim encrypted As Byte()
                Try
                    encrypted = TaskUtils.WaitAndUnwrap(Of Byte())(client.DownloadFileAsync(CloudFilename, credentials))
                Catch ex As AccessDeniedException
                    Throw
                Catch ex As Exception
                    _log.[Error]("下载云端仓库失败", ex)
                    Report(progress, "下载失败：" & ex.Message)
                    Return False
                End Try

                Dim decrypted As Byte()
                Try
                    decrypted = DecryptRepository(encrypted)
                Catch ex As CryptoDecryptionException
                    Report(progress, "解析失败：传输码错误，请检查设置。")
                    Return False
                End Try

                Dim cloudRepo As NoteRepositoryModel = Nothing
                If Not _repositoryStorageService.TryLoadRepositoryFromFile(decrypted, cloudRepo) Then
                    Report(progress, "云端文件格式无效。")
                    Return False
                End If

                ' 检查仓库 ID 是否一致（匹配原版 SilentNotes 行为）
                If localRepo.Id = cloudRepo.Id Then
                    ' 同一仓库——自动合并，无对话框
                    _log.Info(String.Format("同设备同步：本地 {0} 条，云端 {1} 条", localRepo.Notes.Count, cloudRepo.Notes.Count))
                    Return MergeAndSave(localRepo, cloudRepo, progress)
                Else
                    ' 不同仓库——询问用户如何处理
                    _log.Info(String.Format("不同设备同步：本地 ID={0}，云端 ID={1}", localRepo.Id, cloudRepo.Id))
                    Return HandleDifferentRepository(localRepo, cloudRepo, decrypted, progress)
                End If
            Catch ex As AccessDeniedException
                _log.Warning("同步失败：认证被拒")
                Report(progress, "同步失败：用户名或密码错误。")
                Return False
            Catch ex As Exception
                _log.[Error]("同步失败", ex)
                Report(progress, "同步失败：" & ex.Message)
                Return False
            End Try
        End Function

        ''' <summary>
        ''' 合并本地与云端仓库并保存/上传结果。用于同一 ID（同设备）的仓库。
        ''' </summary>
        Private Function MergeAndSave(localRepo As NoteRepositoryModel, cloudRepo As NoteRepositoryModel, progress As Action(Of String)) As Boolean
            Dim merger As New NoteRepositoryMerger()
            Dim mergedRepo As NoteRepositoryModel = merger.Merge(localRepo, cloudRepo)

            Dim localChanged As Boolean = Not RepositoriesAreEqual(mergedRepo, localRepo)
            Dim cloudChanged As Boolean = Not RepositoriesAreEqual(mergedRepo, cloudRepo)

            If Not localChanged AndAlso Not cloudChanged Then
                Report(progress, "同步完成：无需更新。")
                Return True
            End If

            CreateLocalBackup()

            If localChanged Then
                _repositoryStorageService.TrySaveRepository(mergedRepo)
                _log.Info("已保存合并后的本地仓库")
            End If

            If cloudChanged Then
                Report(progress, "正在上传合并后的仓库到云端...")
                Dim credentials As CloudStorageCredentials = GetCredentials()
                Dim encryptedMerged As Byte() = EncryptRepository(mergedRepo)
                Dim diagnostics As New WebDavDiagnostics(_log)
                diagnostics.Upload(CloudFilename, encryptedMerged, credentials)
                _log.Info("已上传合并后的仓库到云端")
            End If

            Report(progress, "同步完成！")
            Return True
        End Function

        ''' <summary>
        ''' 合并/使用云端/取消 的三选一对话框；从后台 Timer 调用时 marshal 到 UI 线程。
        ''' </summary>
        Private Shared Function ShowSyncChoiceDialog(localCount As Integer, cloudCount As Integer) As DialogResult
            Dim message As String =
                "检测到不同设备的仓库（ID 不同），请选择操作：" & vbCrLf & vbCrLf &
                String.Format("本地：{0} 条笔记{1}", localCount, vbCrLf) &
                String.Format("云端：{0} 条笔记{1}{2}", cloudCount, vbCrLf, vbCrLf) &
                "点击「是」合并两个仓库（保留所有笔记）" & vbCrLf &
                "点击「否」使用云端版本（覆盖本地）" & vbCrLf &
                "点击「取消」取消同步"

            Dim show As Func(Of DialogResult) =
                Function() System.Windows.Forms.MessageBox.Show(
                    message,
                    "同步选择",
                    System.Windows.Forms.MessageBoxButtons.YesNoCancel,
                    System.Windows.Forms.MessageBoxIcon.Question)

            Try
                If System.Windows.Forms.Application.OpenForms.Count > 0 Then
                    Dim owner As System.Windows.Forms.Form = System.Windows.Forms.Application.OpenForms(0)
                    If owner IsNot Nothing AndAlso Not owner.IsDisposed AndAlso owner.InvokeRequired Then
                        Return CType(owner.Invoke(show), DialogResult)
                    End If
                End If
            Catch
            End Try
            Return show()
        End Function

        ''' <summary>
        ''' 处理本地与云端仓库 ID 不同时的同步：弹框询问合并/使用云端/取消，
        ''' 匹配原版 SilentNotes 的 ShowMergeChoiceStep 行为。
        ''' </summary>
        Private Function HandleDifferentRepository(
            localRepo As NoteRepositoryModel,
            cloudRepo As NoteRepositoryModel,
            decryptedCloudBytes As Byte(),
            progress As Action(Of String)) As Boolean

            ' VB 把 Notes.Count(predicate) 解析为对 Count 属性做索引，扩展方法须走 Where().Count()
            Dim localCount As Integer = localRepo.Notes.Where(Function(n) Not n.InRecyclingBin).Count()
            Dim cloudCount As Integer = cloudRepo.Notes.Where(Function(n) Not n.InRecyclingBin).Count()

            Dim result As DialogResult = ShowSyncChoiceDialog(localCount, cloudCount)

            If result = DialogResult.Cancel Then
                Report(progress, "同步已取消。")
                Return False
            End If

            If result = DialogResult.Yes Then
                ' 合并两个仓库
                Report(progress, "正在合并仓库...")
                Return MergeAndSave(localRepo, cloudRepo, progress)
            End If

            If result = DialogResult.No Then
                ' 使用云端版本
                Report(progress, "正在使用云端版本...")
                CreateLocalBackup()
                Try
                    Dim location As String = _repositoryStorageService.GetLocation()
                    Dim xmlPath As String = Path.Combine(location, NoteRepositoryModel.RepositoryFileName)
                    File.WriteAllBytes(xmlPath, decryptedCloudBytes)
                    _repositoryStorageService.ClearCache()
                    _log.Info("已使用云端版本覆盖本地")
                    Report(progress, "同步完成！")
                    Return True
                Catch ex As Exception
                    _log.[Error]("保存仓库失败", ex)
                    Report(progress, "保存失败：" & ex.Message)
                    Return False
                End Try
            End If

            Return False
        End Function
    End Class
End Namespace
