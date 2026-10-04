using System;

namespace SilentNotes.Services;

public interface IKeepScreenOn
{
	bool IsActive { get; }

	void Start(TimeSpan duration);

	void Stop();
}
