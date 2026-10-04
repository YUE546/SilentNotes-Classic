using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SilentNotes.Models;

public class NoteModel
{
	public const string CryptorPackageName = "SilentNote";

	public readonly string InstanceId = Guid.NewGuid().ToString();

	public static readonly NoteModel NotFound = new NoteModel
	{
		Id = Guid.Empty
	};

	private Guid _id;

	private string _htmlContent;

	private DateTime? _metaModifiedAt;

	[XmlAttribute(AttributeName = "id")]
	public Guid Id
	{
		get
		{
			return (_id != Guid.Empty) ? _id : (_id = Guid.NewGuid());
		}
		set
		{
			_id = value;
		}
	}

	[XmlAttribute(AttributeName = "note_type")]
	public NoteType NoteType { get; set; }

	[XmlElement(ElementName = "html_content")]
	public string HtmlContent
	{
		get
		{
			return _htmlContent ?? (_htmlContent = string.Empty);
		}
		set
		{
			_htmlContent = value;
		}
	}

	[XmlArray("tags")]
	[XmlArrayItem("tag")]
	public List<string> Tags { get; set; }

	[XmlAttribute(AttributeName = "background_color")]
	public string BackgroundColorHex { get; set; }

	[XmlAttribute(AttributeName = "in_recycling_bin")]
	public bool InRecyclingBin { get; set; }

	[XmlAttribute(AttributeName = "shopping_mode")]
	public bool ShoppingModeActive { get; set; }

	[XmlAttribute(AttributeName = "note_pinned")]
	public bool IsPinned { get; set; }

	[XmlAttribute(AttributeName = "created_at")]
	public DateTime CreatedAt { get; set; }

	[XmlAttribute(AttributeName = "modified_at")]
	public DateTime ModifiedAt { get; set; }

	[XmlIgnore]
	public DateTime? MetaModifiedAt
	{
		get
		{
			if (_metaModifiedAt.HasValue && _metaModifiedAt <= ModifiedAt)
			{
				_metaModifiedAt = null;
			}
			return _metaModifiedAt;
		}
		set
		{
			_metaModifiedAt = value;
		}
	}

	[XmlAttribute(AttributeName = "meta_modified_at")]
	public DateTime MetaModifiedAtSerializeable
	{
		get
		{
			return MetaModifiedAt.Value;
		}
		set
		{
			MetaModifiedAt = value;
		}
	}

	public bool MetaModifiedAtSerializeableSpecified => MetaModifiedAt.HasValue;

	[XmlElement(ElementName = "safe")]
	public Guid? SafeId { get; set; }

	public bool SafeIdSpecified => SafeId.HasValue;

	[XmlElement(ElementName = "attachement_key")]
	public string AttachementKey { get; set; }

	public bool AttachementKeySpecified => !string.IsNullOrEmpty(AttachementKey);

	public NoteModel()
	{
		BackgroundColorHex = "#fbf4c1";
		CreatedAt = DateTime.UtcNow;
		ModifiedAt = CreatedAt;
		MetaModifiedAt = null;
		HtmlContent = string.Empty;
		Tags = new List<string>();
	}

	public void CloneTo(NoteModel target)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		if (this != target)
		{
			target.Id = Id;
			target.NoteType = NoteType;
			target.HtmlContent = HtmlContent;
			target.Tags.Clear();
			target.Tags.AddRange(Tags);
			target.BackgroundColorHex = BackgroundColorHex;
			target.InRecyclingBin = InRecyclingBin;
			target.CreatedAt = CreatedAt;
			target.ModifiedAt = ModifiedAt;
			target.MetaModifiedAt = MetaModifiedAt;
			target.SafeId = SafeId;
			target.ShoppingModeActive = ShoppingModeActive;
			target.IsPinned = IsPinned;
			target.AttachementKey = AttachementKey;
		}
	}

	public void RefreshModifiedAt()
	{
		ModifiedAt = DateTime.UtcNow;
	}

	public void RefreshMetaModifiedAt()
	{
		MetaModifiedAt = DateTime.UtcNow;
	}

	public NoteModel Clone()
	{
		NoteModel noteModel = new NoteModel();
		CloneTo(noteModel);
		return noteModel;
	}
}
