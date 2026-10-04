using System;
using System.IO;
using System.Security;
using System.Text;
using System.Xml.Linq;
using SilentNotes.Models;
using SilentNotes.Workers;
using VanillaCloudStorageClient;

namespace SilentNotes.Services;

public abstract class SettingsServiceBase : ISettingsService
{
	protected readonly IXmlFileService _xmlFileService;

	protected readonly IDataProtectionService _dataProtectionService;

	private SettingsModel _cachedSettings;

	public SettingsServiceBase(IXmlFileService xmlFileService, IDataProtectionService dataProtectionService)
	{
		_xmlFileService = xmlFileService;
		_dataProtectionService = dataProtectionService;
	}

	public SettingsModel LoadSettingsOrDefault()
	{
		if (_cachedSettings != null)
		{
			return _cachedSettings;
		}
		SettingsModel settingsModel = null;
		bool flag = false;
		try
		{
			string filePath = Path.Combine(GetDirectoryPath(), "silentnotes_user_settings.config");
			if (_xmlFileService.TryLoad(filePath, out var xml))
			{
				flag = UpdateSettings(xml);
				settingsModel = XmlUtils.DeserializeFromXmlDocument<SettingsModel>(xml);
				AfterLoading(settingsModel);
			}
		}
		catch (Exception)
		{
			settingsModel = null;
		}
		if (settingsModel == null)
		{
			settingsModel = new SettingsModel();
			flag = true;
		}
		if (flag)
		{
			TrySaveSettingsToLocalDevice(settingsModel);
		}
		_cachedSettings = settingsModel;
		return _cachedSettings;
	}

	public virtual bool TrySaveSettingsToLocalDevice(SettingsModel model)
	{
		try
		{
			BeforeSaving(model);
			string filePath = Path.Combine(GetDirectoryPath(), "silentnotes_user_settings.config");
			bool flag = _xmlFileService.TrySerializeAndSave(filePath, model);
			if (flag)
			{
				_cachedSettings = model;
			}
			return flag;
		}
		catch (Exception)
		{
			return false;
		}
	}

	protected abstract string GetDirectoryPath();

	protected bool UpdateSettings(XDocument xml)
	{
		XElement root = xml.Root;
		XAttribute xAttribute = root.Attribute("revision");
		if (xAttribute == null)
		{
			xAttribute = new XAttribute("revision", 1);
			root.Add(xAttribute);
		}
		int num = int.Parse(xAttribute.Value);
		if (num <= 1)
		{
			UpdateSettingsFrom1To2(root);
		}
		if (num <= 2)
		{
			UpdateSettingsFrom2To3(root);
		}
		bool flag = num < 3;
		if (flag)
		{
			root.SetAttributeValue("revision", 3);
		}
		return flag;
	}

	protected virtual void UpdateSettingsFrom1To2(XElement root)
	{
		XElement xElement = root.Element("cloud_storage");
		if (xElement != null)
		{
			XElement xElement2 = new XElement("cloud_storage_account");
			XElement xElement3 = xElement.Element("cloud_type");
			xElement2.Add(new XElement("cloud_type", xElement3.Value));
			XElement xElement4 = xElement.Element("cloud_url");
			if (xElement4 != null)
			{
				xElement2.Add(new XElement("url", xElement4.Value));
			}
			XElement xElement5 = xElement.Element("cloud_username");
			if (xElement5 != null)
			{
				xElement2.Add(new XElement("username", xElement5.Value));
			}
			root.AddFirst(xElement2);
		}
	}

	protected virtual void UpdateSettingsFrom2To3(XElement root)
	{
		XElement xElement = root.Element("cloud_storage_account");
		if (xElement != null)
		{
			XElement xElement2 = new XElement("cloud_storage_credentials");
			XElement xElement3 = xElement.Element("cloud_type");
			if (xElement3 != null)
			{
				xElement2.Add(new XElement("cloud_storage_id", xElement3.Value.ToLowerInvariant()));
			}
			XElement xElement4 = xElement.Element("username");
			if (xElement4 != null)
			{
				xElement2.Add(new XElement("username", EncryptProperty(xElement4.Value)));
			}
			XElement xElement5 = xElement.Element("protected_password");
			if (xElement5 != null)
			{
				byte[] secretBytes = _dataProtectionService.Unprotect(xElement5.Value);
				SecureString password = SecureStringExtensions.BytesToSecureString(secretBytes, Encoding.Unicode);
				xElement2.Add(new XElement("password", EncryptProperty(password.SecureStringToString())));
			}
			XElement xElement6 = xElement.Element("url");
			if (xElement6 != null)
			{
				xElement2.Add(new XElement("url", xElement6.Value));
			}
			XElement xElement7 = xElement.Element("oauth_access_token");
			if (xElement7 != null)
			{
				xElement2.Add(new XElement("access_token", EncryptProperty(xElement7.Value)));
			}
			root.AddFirst(xElement2);
		}
	}

	protected virtual void BeforeSaving(SettingsModel settings)
	{
		settings.Credentials?.EncryptBeforeSerialization(EncryptProperty);
	}

	protected virtual void AfterLoading(SettingsModel settings)
	{
		settings.Credentials?.DecryptAfterDeserialization(DecryptProperty);
		if (!settings.RememberLastTagFilter)
		{
			settings.FilterTags = null;
		}
	}

	private string EncryptProperty(string plainText)
	{
		return (plainText == null) ? null : _dataProtectionService.Protect(Encoding.UTF8.GetBytes(plainText));
	}

	private string DecryptProperty(string cipherText)
	{
		return (cipherText == null) ? null : Encoding.UTF8.GetString(_dataProtectionService.Unprotect(cipherText));
	}
}
