Option Strict On
Option Explicit On
Imports System
Imports System.IO
Imports Microsoft.Win32

Namespace SilentNotes.WindowsVb.Services
    ''' <summary>
    ''' 数据目录解析与持久化的中央服务。注册表键 HKCU\Software\SilentNotes\DataDirectory
    ''' 是唯一事实来源。
    ''' </summary>
    Friend Module WindowsDataDirectoryService
        Private Const RegistryKeyPath As String = "Software\SilentNotes"
        Private Const RegistryValueName As String = "DataDirectory"

        ''' <summary>从注册表读数据目录路径；键不存在时返回 Nothing。</summary>
        Public Function ReadFromRegistry() As String
            Try
                Using key As RegistryKey = Registry.CurrentUser.OpenSubKey(RegistryKeyPath)
                    If key Is Nothing Then
                        Return Nothing
                    End If
                    Return TryCast(key.GetValue(RegistryValueName), String)
                End Using
            Catch
                Return Nothing
            End Try
        End Function

        ''' <summary>把数据目录路径写入注册表。返回 True 表示写入成功。</summary>
        Public Function WriteToRegistry(path As String) As Boolean
            Try
                Using key As RegistryKey = Registry.CurrentUser.CreateSubKey(RegistryKeyPath)
                    If key IsNot Nothing Then
                        key.SetValue(RegistryValueName, path, RegistryValueKind.String)
                    End If
                End Using
                Return True
            Catch
                Return False
            End Try
        End Function

        ''' <summary>
        ''' 取生效的数据目录：先读注册表；注册表值缺失或指向不存在的目录时回退到 %APPDATA%\SilentNotes。
        ''' </summary>
        Public Function GetEffectiveDirectory() As String
            Dim regPath As String = ReadFromRegistry()
            If Not String.IsNullOrEmpty(regPath) AndAlso Directory.Exists(regPath) Then
                Return regPath
            End If
            Return WindowsApplicationPaths.AppDataDirectory
        End Function

        ''' <summary>注册表里是否存有有效（目录存在）的数据目录路径。</summary>
        Public Function HasValidRegistryPath() As Boolean
            Dim regPath As String = ReadFromRegistry()
            Return Not String.IsNullOrEmpty(regPath) AndAlso Directory.Exists(regPath)
        End Function
    End Module
End Namespace
