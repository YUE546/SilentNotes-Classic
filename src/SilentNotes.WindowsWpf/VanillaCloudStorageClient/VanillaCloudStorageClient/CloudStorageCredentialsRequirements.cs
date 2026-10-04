using System;

namespace VanillaCloudStorageClient;

[Flags]
public enum CloudStorageCredentialsRequirements
{
	None = 0,
	Token = 1,
	Username = 2,
	Password = 4,
	Url = 8,
	Secure = 0x10,
	AcceptUnsafeCertificate = 0x20
}
