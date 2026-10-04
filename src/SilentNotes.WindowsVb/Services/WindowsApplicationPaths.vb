Option Strict On
Option Explicit On
Imports System
Imports System.IO

Namespace SilentNotes.WindowsVb.Services
    Friend Module WindowsApplicationPaths
        Public ReadOnly Property AppDataDirectory As String
            Get
                Dim baseDirectory As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                Dim result As String = Path.Combine(baseDirectory, "SilentNotes")
                Directory.CreateDirectory(result)
                Return result
            End Get
        End Property
    End Module
End Namespace
