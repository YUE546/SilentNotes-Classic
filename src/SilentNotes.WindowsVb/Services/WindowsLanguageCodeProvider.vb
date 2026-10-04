Option Strict On
Option Explicit On
Imports System.Globalization
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsLanguageCodeProvider
        Implements ILanguageCodeProvider

        Public Function GetSystemLanguageCode() As String Implements ILanguageCodeProvider.GetSystemLanguageCode
            Dim name As String = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            If String.IsNullOrWhiteSpace(name) Then
                Return "en"
            End If
            Return name.ToLowerInvariant()
        End Function
    End Class
End Namespace
