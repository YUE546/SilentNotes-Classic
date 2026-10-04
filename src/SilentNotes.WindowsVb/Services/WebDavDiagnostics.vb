Option Strict On
Option Explicit On
Imports System
Imports SilentNotes.Services
Imports VanillaCloudStorageClient
Imports VanillaCloudStorageClient.CloudStorageProviders

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>带基本日志的简化 WebDAV 上传辅助类。</summary>
    Friend Class WebDavDiagnostics
        Private ReadOnly _log As ILogService

        Public Sub New(log As ILogService)
            _log = log
        End Sub

        ''' <summary>带日志地把文件上传到 WebDAV。</summary>
        Public Sub Upload(filename As String, fileContent As Byte(), credentials As CloudStorageCredentials)
            _log.Info(String.Format("上传到 WebDAV: {0} ({1} bytes)", filename, fileContent.Length))

            Dim client As New WebdavCloudStorageClient(False)
            ' 客户端保留 Task 签名（与 C# 版共用 DLL）；本方法运行在后台线程，Wait+解包等待。
            TaskUtils.WaitAndUnwrap(client.UploadFileAsync(filename, fileContent, credentials))

            _log.Info("上传成功")
        End Sub
    End Class
End Namespace
