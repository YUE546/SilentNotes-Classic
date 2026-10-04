Option Strict On
Option Explicit On
Imports System.Net.NetworkInformation
Imports SilentNotes.Services

Namespace SilentNotes.WindowsVb.Services
    Friend Class WindowsInternetStateService
        Implements IInternetStateService

        Public Function IsInternetConnected() As Boolean Implements IInternetStateService.IsInternetConnected
            Try
                Return NetworkInterface.GetIsNetworkAvailable()
            Catch
                Return False
            End Try
        End Function

        Public Function IsInternetCostFree() As Boolean Implements IInternetStateService.IsInternetCostFree
            ' 桌面 Windows 上，除非是按流量计费的连接，否则视为免费连接。
            ' 为简单起见只检查连通性。
            Return IsInternetConnected()
        End Function
    End Class
End Namespace
