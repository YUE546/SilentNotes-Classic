using System;
using System.Xml.Serialization;

namespace SilentNotes.Models;

public class NotificationTriggerModel
{
	[XmlAttribute(AttributeName = "id")]
	public Guid Id { get; set; }

	[XmlAttribute(AttributeName = "created_at")]
	public DateTime CreatedAt { get; set; }

	[XmlIgnore]
	public DateTime? ShownAt { get; set; }

	[XmlAttribute(AttributeName = "shown_at")]
	public DateTime ShownAtSerializeable
	{
		get
		{
			return ShownAt.Value;
		}
		set
		{
			ShownAt = value;
		}
	}

	public bool ShownAtSerializeableSpecified => ShownAt.HasValue;

	public NotificationTriggerModel()
	{
		CreatedAt = DateTime.UtcNow;
	}

	public bool IsDue(DateTime now, TimeSpan queueTime)
	{
		return !ShownAt.HasValue && now - CreatedAt >= queueTime;
	}
}
