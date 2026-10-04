using System.Security.Cryptography;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal sealed class WindowsCryptoRandomService : ICryptoRandomService
    {
        public byte[] GetRandomBytes(int numberOfBytes)
        {
            byte[] result = new byte[numberOfBytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(result);
            return result;
        }
    }
}
