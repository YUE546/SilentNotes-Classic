using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using SilentNotes.Models;
using SilentNotes.Services;
using SilentNotes.WindowsWpf.Services;

namespace SilentNotes.WindowsWpf
{
    public partial class App : Application
    {
        private const string MutexName = "Global\\SilentNotes_WPF_SingleInstance";
        private static Mutex _mutex;

        public static IServiceProvider Services { get; private set; }

        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
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

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                bool isNewInstance;
                _mutex = new Mutex(true, MutexName, out isNewInstance);

                if (!isNewInstance)
                {
                    ActivateExistingInstance();
                    Shutdown();
                    return;
                }

                base.OnStartup(e);
                ServiceCollection services = new ServiceCollection();
                RegisterServices(services);
                Services = services.BuildServiceProvider();
                Ioc.Instance.Initialize(Services);
            }
            catch (Exception ex)
            {
                try
                {
                    string dataDir = WindowsDataDirectoryService.GetEffectiveDirectory();
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(dataDir, "crash.log"),
                        $"[{DateTime.Now}] {ex}\r\n");
                }
                catch { }
                throw;
            }
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

        private static class NativeMethods
        {
            public const int SW_RESTORE = 9;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        }

        private static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ICryptoRandomService, WindowsCryptoRandomService>();
            services.AddSingleton<IDataProtectionService, WindowsDataProtectionService>();
            services.AddSingleton<IEnvironmentService, WindowsEnvironmentService>();
            services.AddSingleton<IFilePickerService, WindowsFilePickerService>();
            services.AddSingleton<IFolderPickerService, WindowsFolderPickerService>();
            services.AddSingleton<IFeedbackService, WpfFeedbackService>();
            services.AddSingleton<IInternetStateService, WindowsInternetStateService>();
            services.AddSingleton<ILogService, WindowsLogService>();
            services.AddSingleton<ISafeKeyService, SafeKeyService>();
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
            services.AddSingleton<WpfThemeService>();
        }
    }
}
