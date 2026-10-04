using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using FluentFTP.Client.BaseClient;
using Flurl;

namespace VanillaCloudStorageClient.CloudStorageProviders;

public class FtpCloudStorageClient : CloudStorageClientBase, ICloudStorageClient
{
	private const int UploadTimeoutSeconds = 40;

	private const int DownloadTimeoutSeconds = 30;

	private readonly IFtpFakeResponse _fakeResponse;

	private FtpProfile _lastConnectionProfile;

	public override CloudStorageCredentialsRequirements CredentialsRequirements => CloudStorageCredentialsRequirements.Username | CloudStorageCredentialsRequirements.Password | CloudStorageCredentialsRequirements.Url | CloudStorageCredentialsRequirements.Secure | CloudStorageCredentialsRequirements.AcceptUnsafeCertificate;

	private bool IsInTestMode => _fakeResponse != null;

	public FtpCloudStorageClient()
		: this(null)
	{
	}

	public FtpCloudStorageClient(IFtpFakeResponse fakeResponse)
	{
		_fakeResponse = fakeResponse;
	}

	public override Task UploadFileAsync(string filename, byte[] fileContent, CloudStorageCredentials credentials)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			Url val = new Url(credentials.Url).AppendPathSegment((object)filename, false);
			FtpClient val2 = new FtpClient(val.Host, new NetworkCredential(credentials.Username, credentials.Password), 0, (FtpConfig)null, (IFtpLogger)null);
			try
			{
				((BaseFtpClient)val2).Config.ValidateAnyCertificate = credentials.AcceptInvalidCertificate;
				((BaseFtpClient)val2).Config.ReadTimeout = 40000;
				if (!IsInTestMode)
				{
					_lastConnectionProfile = ConnectOrAutoConnect(val2, _lastConnectionProfile);
					val2.UploadBytes(fileContent, val.Path, (FtpRemoteExists)4, false, (Action<FtpProgress>)null);
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		catch (Exception catchedException)
		{
			throw CloudStorageClientBase.ConvertToCloudStorageException(catchedException);
		}
		return Task.CompletedTask;
	}

	public override Task<byte[]> DownloadFileAsync(string filename, CloudStorageCredentials credentials)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			Url val = new Url(credentials.Url).AppendPathSegment((object)filename, false);
			FtpClient val2 = new FtpClient(val.Host, new NetworkCredential(credentials.Username, credentials.Password), 0, (FtpConfig)null, (IFtpLogger)null);
			byte[] fakeServerResponseBytes = default(byte[]);
			try
			{
				((BaseFtpClient)val2).Config.ValidateAnyCertificate = credentials.AcceptInvalidCertificate;
				((BaseFtpClient)val2).Config.ReadTimeout = 30000;
				if (IsInTestMode)
				{
					fakeServerResponseBytes = _fakeResponse.GetFakeServerResponseBytes((string)(new Url(credentials.Url).AppendPathSegment((object)filename, false)));
				}
				else
				{
					_lastConnectionProfile = ConnectOrAutoConnect(val2, _lastConnectionProfile);
					val2.DownloadBytes(out fakeServerResponseBytes, val.Path, 0L, (Action<FtpProgress>)null, 0L);
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			return Task.FromResult(fakeServerResponseBytes);
		}
		catch (Exception catchedException)
		{
			throw CloudStorageClientBase.ConvertToCloudStorageException(catchedException);
		}
	}

	public override Task DeleteFileAsync(string filename, CloudStorageCredentials credentials)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			Url val = new Url(credentials.Url).AppendPathSegment((object)filename, false);
			FtpClient val2 = new FtpClient(val.Host, new NetworkCredential(credentials.Username, credentials.Password), 0, (FtpConfig)null, (IFtpLogger)null);
			try
			{
				((BaseFtpClient)val2).Config.ValidateAnyCertificate = credentials.AcceptInvalidCertificate;
				if (!IsInTestMode)
				{
					_lastConnectionProfile = ConnectOrAutoConnect(val2, _lastConnectionProfile);
					val2.DeleteFile(val.Path);
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		catch (Exception catchedException)
		{
			throw CloudStorageClientBase.ConvertToCloudStorageException(catchedException);
		}
		return Task.CompletedTask;
	}

	public override Task<bool> ExistsFileAsync(string filename, CloudStorageCredentials credentials)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			Url val = new Url(credentials.Url).AppendPathSegment((object)filename, false);
			FtpClient val2 = new FtpClient(val.Host, new NetworkCredential(credentials.Username, credentials.Password), 0, (FtpConfig)null, (IFtpLogger)null);
			bool result;
			try
			{
				((BaseFtpClient)val2).Config.ValidateAnyCertificate = credentials.AcceptInvalidCertificate;
				if (IsInTestMode)
				{
					result = _fakeResponse.GetFakeServerExistsFile(credentials.Url);
				}
				else
				{
					_lastConnectionProfile = ConnectOrAutoConnect(val2, _lastConnectionProfile);
					result = val2.FileExists(val.Path);
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			return Task.FromResult(result);
		}
		catch (Exception catchedException)
		{
			throw CloudStorageClientBase.ConvertToCloudStorageException(catchedException);
		}
	}

	public override Task<List<string>> ListFileNamesAsync(CloudStorageCredentials credentials)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		credentials.ThrowIfInvalid(CredentialsRequirements, allowAnonymous: true);
		try
		{
			Url val = new Url(credentials.Url);
			string[] source = null;
			FtpClient val2 = new FtpClient(val.Host, new NetworkCredential(credentials.Username, credentials.Password), 0, (FtpConfig)null, (IFtpLogger)null);
			try
			{
				((BaseFtpClient)val2).Config.ValidateAnyCertificate = credentials.AcceptInvalidCertificate;
				if (IsInTestMode)
				{
					string fakeServerResponseString = _fakeResponse.GetFakeServerResponseString(credentials.Url);
					string text = fakeServerResponseString.Replace("\r\n", "\n");
					source = text.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
				}
				else
				{
					_lastConnectionProfile = ConnectOrAutoConnect(val2, _lastConnectionProfile);
					source = val2.GetNameListing(val.Path?.TrimEnd('/'));
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			List<string> list = source.Select((string fileName) => Path.GetFileName(fileName)).ToList();
			list.Remove("..");
			list.Remove(".");
			return Task.FromResult(list);
		}
		catch (Exception catchedException)
		{
			throw CloudStorageClientBase.ConvertToCloudStorageException(catchedException);
		}
	}

	private static FtpProfile ConnectOrAutoConnect(FtpClient ftpClient, FtpProfile lastProfile)
	{
		if (lastProfile != null && string.Equals(((BaseFtpClient)ftpClient).Host, lastProfile.Host))
		{
			ftpClient.Connect(lastProfile);
			return lastProfile;
		}
		FtpProfile val = ftpClient.AutoConnect();
		if (val == null)
		{
			throw new ConnectionFailedException();
		}
		return val;
	}
}


