// Compat shim: ILogService exists in the previously prebuilt SilentNotes.Core.dll but has
// no source file in src/SilentNotes.AllPlatforms. Recreated here (signature-identical,
// verified by reflecting the old DLL) so the rebuilt assembly keeps the same API surface.
using System;

namespace SilentNotes.Services
{
    /// <summary>
    /// Service to write log messages to the application log.
    /// </summary>
    public interface ILogService
    {
        /// <summary>Logs an informational message.</summary>
        /// <param name="message">The message to log.</param>
        void Info(string message);

        /// <summary>Logs a warning message.</summary>
        /// <param name="message">The message to log.</param>
        void Warning(string message);

        /// <summary>Logs an error message together with its exception.</summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception which caused the error, or null if not available.</param>
        void Error(string message, Exception exception = null);
    }
}
