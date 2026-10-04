using System.Net.NetworkInformation;
using SilentNotes.Services;

namespace SilentNotes.WindowsWpf.Services
{
    internal class WindowsInternetStateService : IInternetStateService
    {
        public bool IsInternetConnected()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false;
            }
        }

        public bool IsInternetCostFree()
        {
            // On desktop Windows, we consider the connection cost-free unless
            // it's a metered connection. For simplicity, we check connectivity.
            return IsInternetConnected();
        }
    }
}