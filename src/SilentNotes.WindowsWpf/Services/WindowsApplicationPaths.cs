using System;
using System.IO;

namespace SilentNotes.WindowsWpf.Services
{
    internal static class WindowsApplicationPaths
    {
        public static string AppDataDirectory
        {
            get
            {
                string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string result = Path.Combine(baseDirectory, "SilentNotes");
                Directory.CreateDirectory(result);
                return result;
            }
        }
    }
}
