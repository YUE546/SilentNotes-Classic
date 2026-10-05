// net40 compat: copy of SilentNotes.AllPlatforms\Services\ISharingService.cs, differs from the
// frozen source only in adding the using directive which the frozen file takes from the
// C#10 global usings file.

using System;
using System.Threading.Tasks;

namespace SilentNotes.Services
{
    /// <summary>
    /// Interface of a service which can share/send data to other apps.
    /// </summary>
    public interface ISharingService
    {
        /// <summary>
        /// Shares the HTML content of a note to other apps.
        /// </summary>
        /// <param name="htmlText">The note content.</param>
        /// <param name="plainText">The note content formatted as plain text.</param>
        /// <param name="subject">The subject/title for sending per email.</param>
        /// <returns>Task for async calls.</returns>
        Task ShareHtmlText(string htmlText, string plainText, string subject);
    }
}
