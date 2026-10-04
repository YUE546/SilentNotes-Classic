Option Strict On
Option Explicit On
Imports System
Imports System.IO
Imports System.Text
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsLogService
        Implements ILogService

        Private Shared ReadOnly LockObj As New Object()
        Private ReadOnly _logFilePath As String
        Private Const MaxFileSize As Long = 5 * 1024 * 1024 ' 5 MB

        Public Sub New()
            Dim directory As String = WindowsDataDirectoryService.GetEffectiveDirectory()
            _logFilePath = Path.Combine(directory, "silentnotes_wpf.log")
            TryRotateIfNeeded()
        End Sub

        Public Sub Info(message As String) Implements ILogService.Info
            Write("INFO", message, Nothing)
        End Sub

        Public Sub Warning(message As String) Implements ILogService.Warning
            Write("WARN", message, Nothing)
        End Sub

        Public Sub [Error](message As String, Optional exception As Exception = Nothing) Implements ILogService.Error
            Write("ERROR", message, exception)
        End Sub

        Private Sub Write(level As String, message As String, exception As Exception)
            Try
                SyncLock LockObj
                    Dim sb As New StringBuilder()
                    sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}", DateTime.Now, level, message)
                    sb.AppendLine()

                    If exception IsNot Nothing Then
                        sb.AppendLine(String.Format("  Exception: {0}", exception.GetType().FullName))
                        sb.AppendLine(String.Format("  Message:   {0}", exception.Message))
                        sb.AppendLine(String.Format("  StackTrace: {0}", exception.StackTrace))
                        If exception.InnerException IsNot Nothing Then
                            sb.AppendLine(String.Format("  InnerException: {0}", exception.InnerException.GetType().FullName))
                            sb.AppendLine(String.Format("  InnerMessage:   {0}", exception.InnerException.Message))
                            sb.AppendLine(String.Format("  InnerStackTrace: {0}", exception.InnerException.StackTrace))
                        End If
                    End If

                    File.AppendAllText(_logFilePath, sb.ToString(), Encoding.UTF8)
                End SyncLock
            Catch
                ' 日志记录绝不能让应用崩溃
            End Try
        End Sub

        Private Sub TryRotateIfNeeded()
            Try
                Dim fileInfo As New FileInfo(_logFilePath)
                If fileInfo.Exists AndAlso fileInfo.Length > MaxFileSize Then
                    Dim rotatedPath As String = _logFilePath & ".1"
                    If File.Exists(rotatedPath) Then
                        File.Delete(rotatedPath)
                    End If
                    File.Move(_logFilePath, rotatedPath)
                End If
            Catch
                ' 轮转失败不影响运行
            End Try
        End Sub
    End Class
End Namespace
