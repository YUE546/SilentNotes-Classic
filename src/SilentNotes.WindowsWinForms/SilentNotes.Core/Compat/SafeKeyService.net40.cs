// Compat copy of SilentNotes.AllPlatforms\Services\SafeKeyService.cs for .NET 4.0.
// Only difference to the frozen original: CloseAllSafes() iterates over a snapshot of
// the key ids. The original enumerates _safeKeys.Keys (a live view) while CloseSafe()
// removes entries from the same dictionary, which always throws
// InvalidOperationException ("Collection was modified") as soon as any safe is open.
using System;
using System.Collections.Generic;
using System.Security;
using SilentNotes.Crypto;
using SilentNotes.Models;

namespace SilentNotes.Services
{
    /// <summary>
    /// Implementation of the <see cref="ISafeKeyService"/> interface.
    /// </summary>
    public class SafeKeyService : ISafeKeyService
    {
        protected readonly Dictionary<Guid, byte[]> _safeKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="SafeKeyService"/> class.
        /// </summary>
        public SafeKeyService()
        {
            _safeKeys = new Dictionary<Guid, byte[]>();
        }

        /// <inheritdoc/>
        public bool TryOpenSafe(SafeModel safe, SecureString password, out bool needsReEncryption)
        {
            needsReEncryption = false;
            if (!_safeKeys.ContainsKey(safe.Id))
            {
                byte[] decryptedKey;
                if (SafeModel.TryDecryptKey(safe.SerializeableKey, password, out decryptedKey, out needsReEncryption))
                    _safeKeys.Add(safe.Id, decryptedKey);
            }
            return IsSafeOpen(safe.Id);
        }

        /// <inheritdoc/>
        public bool TryGetKey(Guid? safeId, out byte[] key)
        {
            if (!safeId.HasValue)
            {
                key = null;
                return false;
            }
            else
                return _safeKeys.TryGetValue(safeId.Value, out key);
        }

        /// <inheritdoc/>
        public void CloseSafe(Guid safeId)
        {
            byte[] key;
            if (_safeKeys.TryGetValue(safeId, out key))
            {
                _safeKeys.Remove(safeId);
                CryptoUtils.CleanArray(key);
            }
        }

        /// <inheritdoc/>
        public bool IsSafeOpen(Guid safeId)
        {
            return _safeKeys.ContainsKey(safeId);
        }

        /// <inheritdoc/>
        public bool CloseAllSafes()
        {
            bool result = _safeKeys.Count > 0;
            // Snapshot the ids: CloseSafe removes from _safeKeys, and enumerating the
            // live Keys collection while removing throws InvalidOperationException.
            List<Guid> openSafeIds = new List<Guid>(_safeKeys.Keys);
            foreach (Guid safeId in openSafeIds)
                CloseSafe(safeId);
            return result;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            CloseAllSafes();
        }
    }
}
