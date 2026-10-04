Option Strict On
Option Explicit On
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports SilentNotes.Services
Imports Forms = System.Windows.Forms

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsFilePickerService
        Implements IFilePickerService

        Private _pickedFilePath As String

        Public Function PickFile(Optional extensions As IEnumerable(Of String) = Nothing) As Task(Of Boolean) _
            Implements IFilePickerService.PickFile

            Using dialog As New Forms.OpenFileDialog()
                dialog.CheckFileExists = True
                dialog.Multiselect = False
                dialog.Filter = BuildFilter(extensions)

                Dim result As Boolean = (dialog.ShowDialog() = Forms.DialogResult.OK)
                If result Then
                    _pickedFilePath = dialog.FileName
                Else
                    _pickedFilePath = Nothing
                End If
                Return TaskUtils.TaskFromResult(Of Boolean)(result)
            End Using
        End Function

        Public Function ReadPickedFile() As Task(Of Byte()) Implements IFilePickerService.ReadPickedFile
            If String.IsNullOrEmpty(_pickedFilePath) OrElse Not File.Exists(_pickedFilePath) Then
                Return TaskUtils.TaskFromResultNull(Of Byte())()
            End If
            Return TaskUtils.TaskFromResult(Of Byte())(File.ReadAllBytes(_pickedFilePath))
        End Function

        Private Shared Function BuildFilter(extensions As IEnumerable(Of String)) As String
            If extensions Is Nothing Then
                Return "All files (*.*)|*.*"
            End If

            Dim normalizedExtensions As String() = extensions.
                Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                Select(Function(item) If(item.StartsWith("."), item, "." & item)).
                Distinct().
                ToArray()

            If (normalizedExtensions Is Nothing) OrElse (normalizedExtensions.Length = 0) Then
                Return "All files (*.*)|*.*"
            End If

            Dim pattern As String = String.Join(";", normalizedExtensions.Select(Function(item) "*" & item))
            Return String.Format("Supported files ({0})|{0}|All files (*.*)|*.*", pattern)
        End Function
    End Class
End Namespace
