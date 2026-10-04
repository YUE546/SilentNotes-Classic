using System;
using System.IO;
using System.Text;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal class WindowsLogService : ILogService
    {
        private static readonly object LockObj = new object();
        private readonly string _logFilePath;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        public WindowsLogService()
        {
            string directory = WindowsDataDirectoryService.GetEffectiveDirectory();
            _logFilePath = Path.Combine(directory, "silentnotes_wpf.log");
            TryRotateIfNeeded();
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warning(string message)
        {
            Write("WARN", message, null);
        }

        public void Error(string message, Exception exception = null)
        {
            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception)
        {
            try
            {
                lock (LockObj)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}",
                        DateTime.Now, level, message);
                    sb.AppendLine();

                    if (exception != null)
                    {
                        sb.AppendLine($"  Exception: {exception.GetType().FullName}");
                        sb.AppendLine($"  Message:   {exception.Message}");
                        sb.AppendLine($"  StackTrace: {exception.StackTrace}");
                        if (exception.InnerException != null)
                        {
                            sb.AppendLine($"  InnerException: {exception.InnerException.GetType().FullName}");
                            sb.AppendLine($"  InnerMessage:   {exception.InnerException.Message}");
                            sb.AppendLine($"  InnerStackTrace: {exception.InnerException.StackTrace}");
                        }
                    }

                    File.AppendAllText(_logFilePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never crash the app
            }
        }

        private void TryRotateIfNeeded()
        {
            try
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Exists && fileInfo.Length > MaxFileSize)
                {
                    string rotatedPath = _logFilePath + ".1";
                    if (File.Exists(rotatedPath))
                        File.Delete(rotatedPath);
                    File.Move(_logFilePath, rotatedPath);
                }
            }
            catch
            {
                // Rotation failure is non-critical
            }
        }
    }
}
