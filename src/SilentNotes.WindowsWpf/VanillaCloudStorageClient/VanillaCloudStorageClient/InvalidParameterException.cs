namespace VanillaCloudStorageClient;

public class InvalidParameterException : CloudStorageException
{
	public string ParameterName { get; private set; }

	public InvalidParameterException(string parameterName)
		: base($"The parameter '{parameterName}' is missing or invalid.", null)
	{
		ParameterName = parameterName;
	}
}
