using System.Threading.Tasks;

namespace VanillaCloudStorageClient;

public interface IOAuth2CloudStorageClient
{
	string BuildAuthorizationRequestUrl(string state, string codeVerifier);

	Task<CloudStorageToken> FetchTokenAsync(string redirectedUrl, string state, string codeVerifier);

	Task<CloudStorageToken> RefreshTokenAsync(CloudStorageToken token);
}
