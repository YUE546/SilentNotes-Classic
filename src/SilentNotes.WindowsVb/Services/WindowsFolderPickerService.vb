Option Strict On
Option Explicit On
Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports SilentNotes.Services
Imports Forms = System.Windows.Forms

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsFolderPickerService
        Implements IFolderPickerService

        Private _pickedFolderPath As String

        Public Function PickFolder() As Task(Of Boolean) Implements IFolderPickerService.PickFolder
            Using dialog As New Forms.FolderBrowserDialog()
                Dim result As Forms.DialogResult = dialog.ShowDialog()
                If result = Forms.DialogResult.OK Then
                    _pickedFolderPath = dialog.SelectedPath
                Else
                    _pickedFolderPath = Nothing
                End If
                Return TaskUtils.TaskFromResult(Of Boolean)(result = Forms.DialogResult.OK)
            End Using
        End Function

        Public Function TrySaveFileToPickedFolder(fileName As String, content As Byte()) As Task(Of Boolean) _
            Implements IFolderPickerService.TrySaveFileToPickedFolder

            If String.IsNullOrEmpty(_pickedFolderPath) OrElse Not Directory.Exists(_pickedFolderPath) Then
                Return TaskUtils.TaskFromResult(Of Boolean)(False)
            End If

            Try
                Dim filePath As String = Path.Combine(_pickedFolderPath, Path.GetFileName(fileName))
                Dim bytes As Byte() = content
                If bytes Is Nothing Then
                    bytes = New Byte(-1) {}
                End If
                File.WriteAllBytes(filePath, bytes)
                Return TaskUtils.TaskFromResult(Of Boolean)(True)
            Catch
                Return TaskUtils.TaskFromResult(Of Boolean)(False)
            End Try
        End Function
    End Class
End Namespace
