namespace SilentNotes.Services;

public interface IDataProtectionService
{
	string Protect(byte[] unprotectedData);

	byte[] Unprotect(string protectedData);
}
