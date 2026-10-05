Option Strict On
Option Explicit On
Imports System
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports SilentNotes
Imports SilentNotes.Services
Imports SilentNotes.WindowsVb.Services
Imports Sunny.UI

Namespace SilentNotes.WindowsVb
    Friend Module Program
        ' 三版共用同一数据目录，两两互斥。
        Private Const MutexName As String = "Global\SilentNotes_WinForms_SingleInstance"
        Private Const WpfMutexName As String = "Global\SilentNotes_WPF_SingleInstance"
        Private _mutex As Mutex

        Public Services As ServiceProvider

        <STAThread()>
        Sub Main()
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)

            ' SunnyUI 控件不继承容器字体；不加这两行会回退到系统默认（宋体）。
            ' 需要不同字号的控件在这之上自行显式设置 Font。
            UIStyles.GlobalFont = True
            UIStyles.GlobalFontName = "Microsoft YaHei UI"

            AddHandler Application.ThreadException, AddressOf OnThreadException
            AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException

            If Not EnsureSingleInstance() Then
                Return
            End If

            Try
                Dim serviceCollection As New ServiceCollection()
                RegisterServices(serviceCollection)
                Services = serviceCollection.BuildServiceProvider()
                Ioc.Instance.Initialize(Services)

                Application.Run(New Views.MainForm())
            Catch ex As Exception
                LogException(ex)
                Throw
            Finally
                If _mutex IsNot Nothing Then
                    Try
                        _mutex.ReleaseMutex()
                    Catch
                    End Try
                    _mutex.Dispose()
                End If
            End Try
        End Sub

        ''' <summary>
        ''' 保证单实例运行：已有本版实例时把它激活到前台；WPF 版运行时拒绝启动（共用数据目录）。
        ''' </summary>
        Private Function EnsureSingleInstance() As Boolean
            Dim isNewInstance As Boolean = False
            _mutex = New Mutex(True, MutexName, isNewInstance)

            If Not isNewInstance Then
                ActivateExistingInstance()
                Return False
            End If

            Try
                Dim wpfMutex As Mutex = Mutex.OpenExisting(WpfMutexName)
                wpfMutex.Dispose()
                MessageBox.Show(
                    "SilentNotes WPF 版正在运行，两者使用同一个数据目录，请先关闭 WPF 版再启动本程序。",
                    "SilentNotes",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning)
                Return False
            Catch ex As Threading.WaitHandleCannotBeOpenedException
                ' WPF 版未运行 → 正常继续
            Catch
                ' 其它异常（如句柄权限）也放行
            End Try
            Return True
        End Function

        Private Sub ActivateExistingInstance()
            Try
                Dim current As Diagnostics.Process = Diagnostics.Process.GetCurrentProcess()
                Dim processes As Diagnostics.Process() = Diagnostics.Process.GetProcessesByName(current.ProcessName)
                Try
                    For Each candidate As Diagnostics.Process In processes
                        If candidate.Id <> current.Id AndAlso candidate.MainWindowHandle <> IntPtr.Zero Then
                            NativeMethods.SetForegroundWindow(candidate.MainWindowHandle)
                            NativeMethods.ShowWindow(candidate.MainWindowHandle, NativeMethods.SW_RESTORE)
                            Exit For
                        End If
                    Next
                Finally
                    current.Dispose()
                    For Each candidate As Diagnostics.Process In processes
                        candidate.Dispose()
                    Next
                End Try
            Catch
            End Try
        End Sub

        Private Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
            LogException(e.Exception)
        End Sub

        Private Sub OnUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
            Dim ex As Exception = TryCast(e.ExceptionObject, Exception)
            If ex IsNot Nothing Then
                LogException(ex)
            End If
        End Sub

        Private Sub LogException(ex As Exception)
            Try
                Dim dataDir As String = WindowsDataDirectoryService.GetEffectiveDirectory()
                Dim logPath As String = Path.Combine(dataDir, "crash.log")
                File.AppendAllText(logPath, String.Format("[{0}] {1}{2}{2}", DateTime.Now, ex, vbCrLf))
            Catch
            End Try
        End Sub

        Private Sub RegisterServices(services As ServiceCollection)
            services.AddSingleton(Of ICryptoRandomService, WindowsCryptoRandomService)()
            services.AddSingleton(Of IDataProtectionService, WindowsDataProtectionService)()
            services.AddSingleton(Of IEnvironmentService, WindowsEnvironmentService)()
            services.AddSingleton(Of IFilePickerService, WindowsFilePickerService)()
            services.AddSingleton(Of IFolderPickerService, WindowsFolderPickerService)()
            services.AddSingleton(Of IInternetStateService, WindowsInternetStateService)()
            services.AddSingleton(Of ILogService, WindowsLogService)()
            services.AddSingleton(Of ISafeKeyService, SafeKeyService)()
            ' ILanguageService 必须保持注册：Core 的 RepositoryStorageServiceBase 构造函数依赖它，
            ' 缺了它仓库服务无法构建——即使 WinForms UI 字符串不做本地化。
            services.AddSingleton(Of ILanguageCodeProvider, WindowsLanguageCodeProvider)()
            services.AddSingleton(Of ILanguageServiceResourceReader, WindowsLanguageServiceResourceReader)()
            services.AddSingleton(Of ILanguageService)(
                Function(provider As IServiceProvider)
                    Dim sp As ServiceProvider = CType(provider, ServiceProvider)
                    Dim languageCodeProvider As ILanguageCodeProvider = sp.GetRequiredService(Of ILanguageCodeProvider)()
                    Dim resourceReader As ILanguageServiceResourceReader = sp.GetRequiredService(Of ILanguageServiceResourceReader)()
                    Return New LanguageService(resourceReader, "SilentNotes", languageCodeProvider.GetSystemLanguageCode())
                End Function)
            services.AddSingleton(Of INativeBrowserService, WindowsNativeBrowserService)()
            services.AddSingleton(Of IRepositoryStorageService, WindowsRepositoryStorageService)()
            services.AddSingleton(Of ISettingsService, WindowsSettingsService)()
            services.AddSingleton(Of IXmlFileService, XmlFileService)()
            services.AddSingleton(Of WinFormsThemeService)()
            services.AddSingleton(Of IFeedbackService, WinFormsFeedbackService)()
        End Sub
    End Module

    Friend Module NativeMethods
        Public Const SW_RESTORE As Integer = 9

        <System.Runtime.InteropServices.DllImport("user32.dll")>
        Public Function SetForegroundWindow(hWnd As IntPtr) As Boolean
        End Function

        <System.Runtime.InteropServices.DllImport("user32.dll")>
        Public Function ShowWindow(hWnd As IntPtr, dwNewLong As Integer) As Boolean
        End Function
    End Module
End Namespace
