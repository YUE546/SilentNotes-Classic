using System;
using System.Runtime.Serialization;
using System.Security;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace VanillaCloudStorageClient;

[DataContract]
public class CloudStorageCredentials : IDisposable
{
	private bool _disposed = false;

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public string CloudStorageId { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public CloudStorageToken Token { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public string Username { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public SecureString Password { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public string Url { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public bool Secure { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public bool AcceptInvalidCertificate { get; set; }

	[XmlIgnore]
	[JsonIgnore]
	[IgnoreDataMember]
	public string UnprotectedPassword
	{
		get
		{
			return Password.SecureStringToString();
		}
		set
		{
			Password = SecureStringExtensions.StringToSecureString(value);
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			Password?.Dispose();
			Password = null;
		}
	}
}
