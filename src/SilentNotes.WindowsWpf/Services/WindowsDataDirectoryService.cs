using System;
using System.IO;
using Microsoft.Win32;

namespace SilentNotes.WindowsWpf.Services
{
    /// <summary>
    /// Central service for resolving and persisting the data directory path via the Windows registry.
    /// The registry key HKCU\Software\SilentNotes\DataDirectory is the single source of truth.
    /// </summary>
    internal static class WindowsDataDirectoryService
    {
        private const string RegistryKeyPath = @"Software\SilentNotes";
        private const string RegistryValueName = "DataDirectory";

        /// <summary>
        /// Reads the data directory path from the registry. Returns null if the key does not exist.
        /// </summary>
        public static string ReadFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    return key?.GetValue(RegistryValueName) as string;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the data directory path to the registry.
        /// </summary>
        /// <returns>True if the write succeeded, false otherwise.</returns>
        public static bool WriteToRegistry(string path)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    key?.SetValue(RegistryValueName, path, RegistryValueKind.String);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the effective data directory. Reads from registry first; falls back to
        /// %APPDATA%\SilentNotes if the registry value is missing or points to a non-existent directory.
        /// </summary>
        public static string GetEffectiveDirectory()
        {
            string regPath = ReadFromRegistry();
            if (!string.IsNullOrEmpty(regPath) && Directory.Exists(regPath))
                return regPath;

            return WindowsApplicationPaths.AppDataDirectory;
        }

        /// <summary>
        /// Checks whether the registry contains a valid (existing) data directory path.
        /// Returns false if the registry key is missing or the path does not exist.
        /// </summary>
        public static bool HasValidRegistryPath()
        {
            string regPath = ReadFromRegistry();
            return !string.IsNullOrEmpty(regPath) && Directory.Exists(regPath);
        }
    }
}
