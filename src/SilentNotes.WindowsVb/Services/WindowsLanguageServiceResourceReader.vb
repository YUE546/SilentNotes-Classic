Option Strict On
Option Explicit On
Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsLanguageServiceResourceReader
        Implements ILanguageServiceResourceReader

        Public Function TryOpenResourceStream(domain As String, languageCode As String) As Task(Of Stream) _
            Implements ILanguageServiceResourceReader.TryOpenResourceStream

            Dim fileName As String = String.Format("Lng.{0}.{1}", domain, languageCode)
            Dim filePath As String = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Resources", "Localization", fileName)

            If Not File.Exists(filePath) Then
                Return TaskUtils.TaskFromResultNull(Of Stream)()
            End If

            Return TaskUtils.TaskFromResult(Of Stream)(File.OpenRead(filePath))
        End Function
    End Class
End Namespace
