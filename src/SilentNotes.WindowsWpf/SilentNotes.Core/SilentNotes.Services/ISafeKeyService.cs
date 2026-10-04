using System;
using System.Security;
using SilentNotes.Models;

namespace SilentNotes.Services;

public interface ISafeKeyService : IDisposable
{
	bool TryOpenSafe(SafeModel safe, SecureString password, out bool needsReEncryption);

	bool TryGetKey(Guid? safeId, out byte[] key);

	void CloseSafe(Guid safeId);

	bool IsSafeOpen(Guid safeId);

	bool CloseAllSafes();
}
