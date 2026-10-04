using System;
using System.Security.Cryptography;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsDataProtectionService : IDataProtectionService
    {
        public string Protect(byte[] unprotectedData)
        {
            byte[] protectedBytes = ProtectedData.Protect(
                unprotectedData,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public byte[] Unprotect(string protectedData)
        {
            byte[] protectedBytes = Convert.FromBase64String(protectedData);
            return ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
        }
    }
}
