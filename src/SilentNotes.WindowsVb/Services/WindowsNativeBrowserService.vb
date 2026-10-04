Option Strict On
Option Explicit On
Imports System.Diagnostics
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsNativeBrowserService
        Implements INativeBrowserService

        Public Sub OpenWebsite(url As String) Implements INativeBrowserService.OpenWebsite
            Dim psi As New ProcessStartInfo(url)
            psi.UseShellExecute = True
            Process.Start(psi)
        End Sub

        Public Sub OpenWebsiteInApp(url As String) Implements INativeBrowserService.OpenWebsiteInApp
            OpenWebsite(url)
        End Sub
    End Class
End Namespace
