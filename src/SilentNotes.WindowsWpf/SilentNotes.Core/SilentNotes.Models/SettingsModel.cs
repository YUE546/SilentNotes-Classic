using System.Collections.Generic;
using System.Xml.Serialization;
using VanillaCloudStorageClient;

namespace SilentNotes.Models;

[XmlRoot(ElementName = "silentnotes_settings")]
public class SettingsModel
{
	public const int NewestSupportedRevision = 3;

	public const string UserSettingsFileName = "silentnotes_user_settings.config";

	public const string StartDefaultNoteColorHex = "#fbf4c1";

	private string _selectedEncryptionAlgorithm;

	private string _selectedKdfAlgorithm;

	private string _transferCode;

	private List<string> _noteColorsHex;

	private List<string> _transferCodeHistory;

	private List<NotificationTriggerModel> _notificationTriggers;

	private List<string> _filterTags;

	[XmlAttribute(AttributeName = "revision")]
	public int Revision { get; set; }

	[XmlElement("cloud_storage_credentials")]
	public SerializeableCloudStorageCredentials Credentials { get; set; }

	[XmlElement("font-family")]
	public string FontFamily { get; set; }

	[XmlElement("font-scale")]
	public double FontScale { get; set; }

	[XmlElement("selected_wallpaper")]
	public string SelectedWallpaper { get; set; }

	[XmlElement("use_wallpaper")]
	public bool UseWallpaper { get; set; }

	[XmlElement("use_solid_color_theme")]
	public bool UseSolidColorTheme { get; set; }

	[XmlElement("color_for_solid_theme")]
	public string ColorForSolidTheme { get; set; }

	[XmlElement("theme_mode")]
	public ThemeMode ThemeMode { get; set; }

	[XmlElement("use_color_for_all_notes_dark")]
	public bool UseColorForAllNotesInDarkMode { get; set; }

	[XmlElement("color_for_all_notes_dark")]
	public string ColorForAllNotesInDarkModeHex { get; set; }

	[XmlIgnore]
	public List<string> NoteColorsHex
	{
		get
		{
			List<string> list = _noteColorsHex;
			if (list == null)
			{
				List<string> obj = new List<string>
				{
					"#ffffff", "#fbf4c1", "#fdd8bb", "#facbc6", "#fcd5ef", "#d9d9fc", "#cee7fb", "#d0f8f9", "#d9f8c8", "#ae7f0a",
					"#871908", "#800080", "#0d5696", "#007a7a", "#33750f", "#333333"
				};
				List<string> list2 = obj;
				_noteColorsHex = obj;
				list = list2;
			}
			return list;
		}
	}

	[XmlElement("default_note_color")]
	public string DefaultNoteColorHex { get; set; }

	[XmlElement("note_max_height_scale")]
	public double NoteMaxHeightScale { get; set; }

	[XmlElement("keep_screen_up_duration")]
	public int KeepScreenUpDuration { get; set; }

	[XmlElement("default_note_insertion")]
	public NoteInsertionMode DefaultNoteInsertion { get; set; }

	[XmlElement("start_with_tags_open")]
	public bool StartWithTagsOpen { get; set; }

	[XmlElement("remember_last_tag_filter")]
	public bool RememberLastTagFilter { get; set; }

	[XmlElement("hide_closed_safe_notes")]
	public bool HideClosedSafeNotes { get; set; }

	[XmlElement("selected_encryption_algorithm")]
	public string SelectedEncryptionAlgorithm
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_selectedEncryptionAlgorithm))
			{
				_selectedEncryptionAlgorithm = GetDefaultEncryptionAlgorithmName();
			}
			return _selectedEncryptionAlgorithm;
		}
		set
		{
			_selectedEncryptionAlgorithm = value;
		}
	}

	public string SelectedKdfAlgorithm
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_selectedKdfAlgorithm))
			{
				_selectedKdfAlgorithm = GetDefaultKdfAlgorithmName();
			}
			return _selectedKdfAlgorithm;
		}
		set
		{
			_selectedKdfAlgorithm = value;
		}
	}

	[XmlElement("auto_sync_mode")]
	public AutoSynchronizationMode AutoSyncMode { get; set; }

	[XmlElement("transfer_code")]
	public string TransferCode
	{
		get
		{
			return _transferCode;
		}
		set
		{
			if (!string.IsNullOrWhiteSpace(value) && !string.Equals(_transferCode, value))
			{
				TransferCodeHistory.Remove(value);
				if (!string.IsNullOrWhiteSpace(_transferCode))
				{
					TransferCodeHistory.Remove(_transferCode);
					TransferCodeHistory.Insert(0, _transferCode);
				}
				_transferCode = value;
			}
		}
	}

	[XmlArray("transfer_code_history")]
	[XmlArrayItem("transfer_code")]
	public List<string> TransferCodeHistory
	{
		get
		{
			return _transferCodeHistory ?? (_transferCodeHistory = new List<string>());
		}
		set
		{
			_transferCodeHistory = value;
		}
	}

	[XmlElement("prevent_screenshots")]
	public bool PreventScreenshots { get; set; }

	[XmlElement("always_english")]
	public bool AlwaysEnglish { get; set; }

	public bool HasCloudStorageClient => Credentials?.CloudStorageId != null;

	public bool HasTransferCode => !string.IsNullOrWhiteSpace(TransferCode);

	[XmlElement("filter_tags")]
	public List<string> FilterTags
	{
		get
		{
			return _filterTags ?? (_filterTags = new List<string>());
		}
		set
		{
			_filterTags = value;
		}
	}

	[XmlArray("notification_triggers")]
	[XmlArrayItem("notification_trigger")]
	public List<NotificationTriggerModel> NotificationTriggers
	{
		get
		{
			return _notificationTriggers ?? (_notificationTriggers = new List<NotificationTriggerModel>());
		}
		set
		{
			_notificationTriggers = value;
		}
	}

	[XmlElement("data_directory")]
	public string DataDirectory { get; set; }

	[XmlIgnore]
	public string Filter { get; set; }

	public SettingsModel()
	{
		Revision = 3;
		AutoSyncMode = AutoSynchronizationMode.CostFreeInternetOnly;
		FontScale = 1.0;
		UseSolidColorTheme = false;
		ColorForSolidTheme = "#3e3e3e";
		DefaultNoteColorHex = "#fbf4c1";
		NoteMaxHeightScale = 1.0;
		DefaultNoteInsertion = NoteInsertionMode.AtTop;
		UseColorForAllNotesInDarkMode = false;
		ColorForAllNotesInDarkModeHex = "#323232";
		KeepScreenUpDuration = 15;
		UseWallpaper = true;
		RememberLastTagFilter = false;
		AlwaysEnglish = false;
	}

	public static string GetDefaultEncryptionAlgorithmName()
	{
		return "xchacha20_poly1305";
	}

	public static string GetDefaultKdfAlgorithmName()
	{
		return "argon2id";
	}
}
