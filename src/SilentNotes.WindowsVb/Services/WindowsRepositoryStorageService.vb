Option Strict On
Option Explicit On
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsRepositoryStorageService
        Inherits RepositoryStorageServiceBase

        Public Sub New(xmlFileService As IXmlFileService, languageService As ILanguageService)
            MyBase.New(xmlFileService, languageService)
        End Sub

        Public Overrides Function GetLocation() As String
            Return WindowsDataDirectoryService.GetEffectiveDirectory()
        End Function
    End Class
End Namespace
