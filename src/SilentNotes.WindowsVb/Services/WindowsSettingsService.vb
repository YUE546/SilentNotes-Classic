Option Strict On
Option Explicit On
Imports System
Imports SilentNotes.Models
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsSettingsService
        Inherits SettingsServiceBase

        Public Sub New(xmlFileService As IXmlFileService, dataProtectionService As IDataProtectionService)
            MyBase.New(xmlFileService, dataProtectionService)
        End Sub

        Protected Overrides Function GetDirectoryPath() As String
            Return WindowsDataDirectoryService.GetEffectiveDirectory()
        End Function

        Public Overrides Function TrySaveSettingsToLocalDevice(model As SettingsModel) As Boolean
            Dim newPath As String = model.DataDirectory
            If Not String.IsNullOrEmpty(newPath) Then
                WindowsDataDirectoryService.WriteToRegistry(newPath)
            End If
            Return MyBase.TrySaveSettingsToLocalDevice(model)
        End Function
    End Class
End Namespace
