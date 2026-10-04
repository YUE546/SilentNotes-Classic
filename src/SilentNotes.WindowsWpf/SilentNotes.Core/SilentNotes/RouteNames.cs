using System.Text;
using System.Web;

namespace SilentNotes;

public static class RouteNames
{
	public const string NoteRepository = "/noterepository";

	public const string Note = "/note";

	public const string Checklist = "/checklist";

	public const string ChangePassword = "/changepassword";

	public const string CloudStorageAccount = "/cloudstorageaccount";

	public const string CloudStorageChoice = "/cloudstoragechoice";

	public const string CloudStorageOauthWaiting = "/cloudstorageoauthwaiting";

	public const string Export = "/export";

	public const string Import = "/import";

	public const string FirstTimeSync = "/firsttimesync";

	public const string Info = "/info";

	public const string MergeChoice = "/mergechoice";

	public const string OpenSafe = "/opensafe";

	public const string RecycleBin = "/recyclebin";

	public const string Settings = "/settings";

	public const string TransferCodePrompt = "/transfercodeprompt";

	public const string TransferCodeHistory = "/transfercodehistory";

	public static string Combine(string routeName, params object[] parts)
	{
		char c = '/';
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(routeName);
		foreach (object obj in parts)
		{
			if (obj != null)
			{
				string value = HttpUtility.UrlEncode(obj.ToString());
				if (stringBuilder[stringBuilder.Length - 1] != c)
				{
					stringBuilder.Append(c);
				}
				stringBuilder.Append(value);
			}
		}
		return stringBuilder.ToString();
	}
}
