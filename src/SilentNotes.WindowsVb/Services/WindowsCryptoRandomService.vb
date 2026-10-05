Option Strict On
Option Explicit On
Imports System.Security.Cryptography
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsCryptoRandomService
        Implements ICryptoRandomService

        ' GetBytes 线程安全，实例可整个应用生命周期复用。
        Private Shared ReadOnly Rng As RandomNumberGenerator = RandomNumberGenerator.Create()

        Public Function GetRandomBytes(numberOfBytes As Integer) As Byte() Implements ICryptoRandomService.GetRandomBytes
            Dim result As Byte() = New Byte(numberOfBytes - 1) {}
            Rng.GetBytes(result)
            Return result
        End Function
    End Class
End Namespace
