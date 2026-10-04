using System;

namespace SilentNotes.Services;

public interface ISynchronizationState
{
	bool IsSynchronizationRunning { get; }

	DateTime? LastFinishedSynchronization { get; }

	void UpdateLastFinishedSynchronization();

	bool TryStartSynchronizationState(SynchronizationType syncType);

	void StopSynchronizationState();
}
