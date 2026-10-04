Option Strict On
Option Explicit On
Imports System.Security.Cryptography
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsCryptoRandomService
        Implements ICryptoRandomService

        Public Function GetRandomBytes(numberOfBytes As Integer) As Byte() Implements ICryptoRandomService.GetRandomBytes
            Dim result As Byte() = New Byte(numberOfBytes - 1) {}
            Using rng As RandomNumberGenerator = RandomNumberGenerator.Create()
                rng.GetBytes(result)
            End Using
            Return result
        End Function
    End Class
End Namespace
