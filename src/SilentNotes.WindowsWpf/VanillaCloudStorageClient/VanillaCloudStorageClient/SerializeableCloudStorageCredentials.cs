using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace VanillaCloudStorageClient;

[Serializable]
[XmlType("cloud_storage_credentials")]
[DataContract(Name = "cloud_storage_credentials")]
public class SerializeableCloudStorageCredentials : CloudStorageCredentials
{
	[XmlElement("cloud_storage_id")]
	[JsonPropertyName("cloud_storage_id")]
	[DataMember(Name = "cloud_storage_id")]
	public string SerializeableCloudStorageId { get; set; }

	[XmlElement("access_token")]
	[JsonPropertyName("access_token")]
	[DataMember(EmitDefaultValue = false, Name = "access_token")]
	public string SerializeableAccessToken { get; set; }

	[JsonIgnore]
	public bool SerializeableAccessTokenSpecified => SerializeableAccessToken != null;

	[XmlElement("token_expiry_date")]
	[JsonPropertyName("token_expiry_date")]
	[DataMember(EmitDefaultValue = false, Name = "token_expiry_date")]
	public DateTime? SerializeableExpiryDate { get; set; }

	[JsonIgnore]
	public bool SerializeableExpiryDateSpecified => SerializeableExpiryDate.HasValue;

	[XmlElement("refresh_token")]
	[JsonPropertyName("refresh_token")]
	[DataMember(EmitDefaultValue = false, Name = "refresh_token")]
	public string SerializeableRefreshToken { get; set; }

	[JsonIgnore]
	public bool SerializeableRefreshTokenSpecified => SerializeableRefreshToken != null;

	[XmlElement("username")]
	[JsonPropertyName("username")]
	[DataMember(EmitDefaultValue = false, Name = "username")]
	public string SerializeableUsername { get; set; }

	[JsonIgnore]
	public bool SerializeableUsernameSpecified => SerializeableUsername != null;

	[XmlElement("password")]
	[JsonPropertyName("password")]
	[DataMember(EmitDefaultValue = false, Name = "password")]
	public string SerializeablePassword { get; set; }

	[JsonIgnore]
	public bool SerializeablePasswordSpecified => SerializeablePassword != null;

	[XmlElement("url")]
	[JsonPropertyName("url")]
	[DataMember(EmitDefaultValue = false, Name = "url")]
	public string SerializeableUrl { get; set; }

	[JsonIgnore]
	public bool SerializeableUrlSpecified => SerializeableUrl != null;

	[XmlElement("secure")]
	[JsonPropertyName("secure")]
	[DataMember(EmitDefaultValue = false, Name = "secure")]
	public bool SerializeableSecure { get; set; }

	[JsonIgnore]
	public bool SerializeableSecureSpecified => SerializeableSecure;

	[XmlElement("accept_invalid_certificate")]
	[JsonPropertyName("accept_invalid_certificate")]
	[DataMember(EmitDefaultValue = false, Name = "accept_invalid_certificate")]
	public bool SerializeableAcceptInvalidCertificate { get; set; }

	[JsonIgnore]
	public bool SerializeableAcceptInvalidCertificateSpecified => SerializeableAcceptInvalidCertificate;

	public void EncryptBeforeSerialization(Func<string, string> encrypt)
	{
		SerializeableCloudStorageId = base.CloudStorageId;
		SerializeableAccessToken = EncryptProperty(base.Token?.AccessToken, encrypt);
		SerializeableExpiryDate = base.Token?.ExpiryDate;
		SerializeableRefreshToken = EncryptProperty(base.Token?.RefreshToken, encrypt);
		SerializeableUsername = EncryptProperty(base.Username, encrypt);
		SerializeablePassword = EncryptProperty(base.UnprotectedPassword, encrypt);
		SerializeableUrl = base.Url;
		SerializeableSecure = base.Secure;
		SerializeableAcceptInvalidCertificate = base.AcceptInvalidCertificate;
	}

	public void DecryptAfterDeserialization(Func<string, string> decrypt)
	{
		CloudStorageToken cloudStorageToken = new CloudStorageToken
		{
			AccessToken = DecryptProperty(SerializeableAccessToken, decrypt),
			ExpiryDate = SerializeableExpiryDate,
			RefreshToken = DecryptProperty(SerializeableRefreshToken, decrypt)
		};
		if (cloudStorageToken.AccessToken == null && cloudStorageToken.RefreshToken == null)
		{
			cloudStorageToken = null;
		}
		base.CloudStorageId = SerializeableCloudStorageId;
		base.Token = cloudStorageToken;
		base.Username = DecryptProperty(SerializeableUsername, decrypt);
		base.UnprotectedPassword = DecryptProperty(SerializeablePassword, decrypt);
		base.Url = SerializeableUrl;
		base.Secure = SerializeableSecure;
		base.AcceptInvalidCertificate = SerializeableAcceptInvalidCertificate;
	}

	private string EncryptProperty(string plainText, Func<string, string> encrypt)
	{
		return (plainText == null) ? null : encrypt(plainText);
	}

	private string DecryptProperty(string cipherText, Func<string, string> decrypt)
	{
		return (cipherText == null) ? null : decrypt(cipherText);
	}
}
