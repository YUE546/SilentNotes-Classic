using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using SilentNotes.Models;
using SilentNotes.Services;
using SilentNotes.WindowsWinForms.Services;
using Sunny.UI;

namespace SilentNotes.WindowsWinForms
{
    internal static class Program
    {
        private const string MutexName = "Global\\SilentNotes_WinForms_SingleInstance";
        private const string WpfMutexName = "Global\\SilentNotes_WPF_SingleInstance";
        private static Mutex _mutex;

        public static IServiceProvider Services { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // SunnyUI controls do not inherit the container font; without this they
            // fall back to the system default (SimSun). Controls that need a different
            // size set Font explicitly on top of this.
            UIStyles.GlobalFont = true;
            UIStyles.GlobalFontName = "Microsoft YaHei UI";

            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            if (!EnsureSingleInstance())
                return;

            try
            {
                ServiceCollection services = new ServiceCollection();
                RegisterServices(services);
                Services = services.BuildServiceProvider();
                Ioc.Instance.Initialize(Services);

                Application.Run(new Views.MainForm());
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }
            finally
            {
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Dispose();
                }
            }
        }

        /// <summary>
        /// Guarantees a single WinForms instance and refuses to start while the WPF
        /// edition is running, because both editions share the same data directory.
        /// </summary>
        private static bool EnsureSingleInstance()
        {
            bool isNewInstance;
            _mutex = new Mutex(true, MutexName, out isNewInstance);

            if (!isNewInstance)
            {
                ActivateExistingInstance();
                return false;
            }

            try
            {
                Mutex wpfMutex;
                if (Mutex.TryOpenExisting(WpfMutexName, out wpfMutex))
                {
                    wpfMutex.Dispose();
                    MessageBox.Show(
                        "SilentNotes WPF 版正在运行，两者使用同一个数据目录，请先关闭 WPF 版再启动本程序。",
                        "SilentNotes",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
            catch
            {
            }
            return true;
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var process in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                    {
                        NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                        NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
                        break;
                    }
                }
            }
            catch { }
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            LogException(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null)
                LogException(ex);
        }

        private static void LogException(Exception ex)
        {
            try
            {
                string dataDir = WindowsDataDirectoryService.GetEffectiveDirectory();
                string logPath = System.IO.Path.Combine(dataDir, "crash.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] {ex}\r\n\r\n");
            }
            catch { }
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ICryptoRandomService, WindowsCryptoRandomService>();
            services.AddSingleton<IDataProtectionService, WindowsDataProtectionService>();
            services.AddSingleton<IEnvironmentService, WindowsEnvironmentService>();
            services.AddSingleton<IFilePickerService, WindowsFilePickerService>();
            services.AddSingleton<IFolderPickerService, WindowsFolderPickerService>();
            services.AddSingleton<IFeedbackService, WinFormsFeedbackService>();
            services.AddSingleton<IInternetStateService, WindowsInternetStateService>();
            services.AddSingleton<ILogService, WindowsLogService>();
            services.AddSingleton<ISafeKeyService, SafeKeyService>();
            // ILanguageService must stay registered: Core's RepositoryStorageServiceBase
            // takes it as a constructor dependency, so the repository service cannot be
            // built without it — even though the WinForms UI strings are not localized.
            services.AddSingleton<ILanguageCodeProvider, WindowsLanguageCodeProvider>();
            services.AddSingleton<ILanguageServiceResourceReader, WindowsLanguageServiceResourceReader>();
            services.AddSingleton<ILanguageService>(provider =>
            {
                ILanguageCodeProvider languageCodeProvider = provider.GetRequiredService<ILanguageCodeProvider>();
                ILanguageServiceResourceReader resourceReader = provider.GetRequiredService<ILanguageServiceResourceReader>();
                return new LanguageService(resourceReader, "SilentNotes", languageCodeProvider.GetSystemLanguageCode());
            });
            services.AddSingleton<INativeBrowserService, WindowsNativeBrowserService>();
            services.AddSingleton<IRepositoryStorageService, WindowsRepositoryStorageService>();
            services.AddSingleton<ISettingsService, WindowsSettingsService>();
            services.AddSingleton<IXmlFileService, XmlFileService>();
            services.AddSingleton<WinFormsThemeService>();
        }

        private static class NativeMethods
        {
            public const int SW_RESTORE = 9;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        }
    }
}
