Option Strict On
Option Explicit On
Imports System
Imports System.Security.Cryptography
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsDataProtectionService
        Implements IDataProtectionService

        Public Function Protect(unprotectedData As Byte()) As String Implements IDataProtectionService.Protect
            Dim protectedBytes As Byte() = ProtectedData.Protect(
                unprotectedData,
                Nothing,
                DataProtectionScope.CurrentUser)
            Return Convert.ToBase64String(protectedBytes)
        End Function

        Public Function Unprotect(data As String) As Byte() Implements IDataProtectionService.Unprotect
            Dim protectedBytes As Byte() = Convert.FromBase64String(data)
            Return ProtectedData.Unprotect(
                protectedBytes,
                Nothing,
                DataProtectionScope.CurrentUser)
        End Function
    End Class
End Namespace
