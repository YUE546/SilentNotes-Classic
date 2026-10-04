using System;

namespace VanillaCloudStorageClient;

public class CloudStorageToken
{
	private DateTime? _expiryDate;

	public string AccessToken { get; set; }

	public DateTime? ExpiryDate
	{
		get
		{
			return _expiryDate;
		}
		set
		{
			if (value.HasValue)
			{
				_expiryDate = value.Value.ToUniversalTime();
			}
			else
			{
				_expiryDate = null;
			}
		}
	}

	public string RefreshToken { get; set; }
}
