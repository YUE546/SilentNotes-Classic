using System;

namespace SilentNotes;

public class AfterResumeMessage
{
	public DateTime LastPauseTime { get; set; }

	public bool SafesClosed { get; set; }
}
