using System;
using System.Collections.Generic;
using System.Security;
using SilentNotes.Crypto;
using SilentNotes.Models;

namespace SilentNotes.Services;

public class SafeKeyService : ISafeKeyService, IDisposable
{
	protected readonly Dictionary<Guid, byte[]> _safeKeys;

	public SafeKeyService()
	{
		_safeKeys = new Dictionary<Guid, byte[]>();
	}

	public bool TryOpenSafe(SafeModel safe, SecureString password, out bool needsReEncryption)
	{
		needsReEncryption = false;
		if (!_safeKeys.ContainsKey(safe.Id) && SafeModel.TryDecryptKey(safe.SerializeableKey, password, out var key, out needsReEncryption))
		{
			_safeKeys.Add(safe.Id, key);
		}
		return IsSafeOpen(safe.Id);
	}

	public bool TryGetKey(Guid? safeId, out byte[] key)
	{
		if (!safeId.HasValue)
		{
			key = null;
			return false;
		}
		return _safeKeys.TryGetValue(safeId.Value, out key);
	}

	public void CloseSafe(Guid safeId)
	{
		if (_safeKeys.TryGetValue(safeId, out var value))
		{
			_safeKeys.Remove(safeId);
			CryptoUtils.CleanArray(value);
		}
	}

	public bool IsSafeOpen(Guid safeId)
	{
		return _safeKeys.ContainsKey(safeId);
	}

	public bool CloseAllSafes()
	{
		bool result = _safeKeys.Count > 0;
		List<Guid> list = new List<Guid>(_safeKeys.Keys);
		foreach (Guid item in list)
		{
			CloseSafe(item);
		}
		return result;
	}

	public void Dispose()
	{
		CloseAllSafes();
	}
}
