using System;
using System.Security;
using System.Xml.Serialization;
using SilentNotes.Crypto;
using SilentNotes.Crypto.KeyDerivation;

namespace SilentNotes.Models;

public class SafeModel
{
	public const string CryptorPackageName = "SilentSafe";

	private Guid _id;

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

	[XmlAttribute(AttributeName = "created_at")]
	public DateTime CreatedAt { get; set; }

	[XmlAttribute(AttributeName = "modified_at")]
	public DateTime ModifiedAt { get; set; }

	[XmlIgnore]
	public DateTime? MaintainedAt { get; set; }

	[XmlAttribute(AttributeName = "maintained_at")]
	public DateTime MaintainedAtSerializeable
	{
		get
		{
			return MaintainedAt.Value;
		}
		set
		{
			MaintainedAt = value;
		}
	}

	public bool MaintainedAtSerializeableSpecified => MaintainedAt.HasValue && MaintainedAt > ModifiedAt;

	[XmlElement("key")]
	public string SerializeableKey { get; set; }

	public SafeModel()
	{
		CreatedAt = DateTime.UtcNow;
		ModifiedAt = CreatedAt;
	}

	public void ClearMaintainedAtIfObsolete()
	{
		if (MaintainedAt.HasValue && MaintainedAt < ModifiedAt)
		{
			MaintainedAt = null;
		}
	}

	public void RefreshModifiedAt()
	{
		ModifiedAt = DateTime.UtcNow;
	}

	public static bool TryDecryptKey(string serializeableKey, SecureString password, out byte[] key, out bool needsReEncryption)
	{
		try
		{
			byte[] packedCipher = CryptoUtils.Base64StringToBytes(serializeableKey);
			ICryptor cryptor = new Cryptor("SilentSafe", null);
			key = cryptor.Decrypt(packedCipher, password, out needsReEncryption);
			return true;
		}
		catch (Exception)
		{
			key = null;
			needsReEncryption = false;
			return false;
		}
	}

	public static string EncryptKey(byte[] key, SecureString password, ICryptoRandomSource randomSource, string encryptionAlgorithm, string kdfAlgorithm)
	{
		ICryptor cryptor = new Cryptor("SilentSafe", randomSource);
		byte[] bytes = cryptor.Encrypt(key, password, KeyDerivationCostType.High, encryptionAlgorithm, kdfAlgorithm);
		return CryptoUtils.BytesToBase64String(bytes);
	}

	public SafeModel Clone()
	{
		SafeModel safeModel = new SafeModel();
		CloneTo(safeModel);
		return safeModel;
	}

	public void CloneTo(SafeModel target)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		if (this != target)
		{
			target.Id = Id;
			target.SerializeableKey = SerializeableKey;
			target.CreatedAt = CreatedAt;
			target.ModifiedAt = ModifiedAt;
			target.MaintainedAt = MaintainedAt;
		}
	}
}
