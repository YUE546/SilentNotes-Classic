#define DEBUG
using System;
using System.Diagnostics;

namespace SilentNotes.Services;

public class SynchronizationState : ISynchronizationState
{
	private readonly object _lock = new object();

	private readonly IMessengerService _messenger;

	private SynchronizationType? _currentSynchronizationType;

	public bool IsSynchronizationRunning => _currentSynchronizationType.HasValue;

	public DateTime? LastFinishedSynchronization { get; private set; }

	public SynchronizationState(IMessengerService messenger)
	{
		_messenger = messenger;
	}

	public void UpdateLastFinishedSynchronization()
	{
		Debug.WriteLine("*** SynchronizationState.UpdateLastFinishedSynchronization()");
		LastFinishedSynchronization = DateTime.UtcNow;
	}

	public bool TryStartSynchronizationState(SynchronizationType syncType)
	{
		Debug.WriteLine("*** SynchronizationState.TryStartSynchronizationState()");
		lock (_lock)
		{
			if (IsSynchronizationRunning)
			{
				return false;
			}
			_currentSynchronizationType = syncType;
			if (ShouldSendChangedMessage(_currentSynchronizationType.Value))
			{
				_messenger?.Send(new SynchronizationIsRunningChangedMessage(isRunning: true));
			}
			return true;
		}
	}

	public void StopSynchronizationState()
	{
		Debug.WriteLine("*** SynchronizationState.StopSynchronizationState()");
		lock (_lock)
		{
			if (IsSynchronizationRunning)
			{
				bool flag = ShouldSendChangedMessage(_currentSynchronizationType.Value);
				_currentSynchronizationType = null;
				if (flag)
				{
					_messenger?.Send(new SynchronizationIsRunningChangedMessage(isRunning: false));
				}
			}
		}
	}

	private bool ShouldSendChangedMessage(SynchronizationType syncType)
	{
		return syncType == SynchronizationType.AtStartup || syncType == SynchronizationType.Manually;
	}
}
