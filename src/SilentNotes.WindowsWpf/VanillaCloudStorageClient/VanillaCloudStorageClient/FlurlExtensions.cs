using System.Net;
using Flurl.Http;

namespace VanillaCloudStorageClient;

public static class FlurlExtensions
{
	public static T WithBasicAuthOrAnonymous<T>(this T clientOrRequest, string username, string password) where T : IFlurlRequest
	{
		if (string.IsNullOrEmpty(username))
		{
			return clientOrRequest;
		}
		return HeaderExtensions.WithBasicAuth<T>(clientOrRequest, username, password);
	}

	public static HttpStatusCode GetHttpStatusCode(this FlurlHttpException flurlHttpException, HttpStatusCode undefinedStatusCode = HttpStatusCode.OK)
	{
		if (flurlHttpException.StatusCode.HasValue)
		{
			return (HttpStatusCode)flurlHttpException.StatusCode.Value;
		}
		return undefinedStatusCode;
	}
}
