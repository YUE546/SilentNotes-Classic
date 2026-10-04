using System;
using System.Text;
using SilentNotes.Services;

namespace SilentNotes.Workers;

public static class TransferCode
{
	private const int CodeLength = 16;

	public static bool IsCodeSet(string code)
	{
		return !string.IsNullOrWhiteSpace(code);
	}

	public static string GenerateCode(ICryptoRandomService randomSource)
	{
		return GenerateCode(16, randomSource);
	}

	public static string GenerateCode(int length, ICryptoRandomService randomSource)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = length;
		do
		{
			int numberOfBytes = (int)Math.Floor((double)num * 3.0 / 4.0 + 1.0);
			byte[] randomBytes = randomSource.GetRandomBytes(numberOfBytes);
			string text = Convert.ToBase64String(randomBytes);
			string text2 = text;
			foreach (char c in text2)
			{
				if (UnmixableAlphabet.IsOfCorrectAlphabet(c))
				{
					stringBuilder.Append(c);
				}
			}
			num = length - stringBuilder.Length;
		}
		while (num > 0);
		if (stringBuilder.Length > length)
		{
			stringBuilder.Remove(length, stringBuilder.Length - length);
		}
		return stringBuilder.ToString();
	}

	public static bool TrySanitizeUserInput(string code, out string sanitizedCode)
	{
		sanitizedCode = null;
		if (string.IsNullOrWhiteSpace(code))
		{
			return false;
		}
		sanitizedCode = code.Replace(" ", string.Empty).Replace("-", string.Empty);
		if (sanitizedCode.Length != 16)
		{
			return false;
		}
		if (!UnmixableAlphabet.IsOfCorrectAlphabet(sanitizedCode))
		{
			return false;
		}
		return true;
	}

	public static string FormatTransferCodeForDisplay(string transferCode)
	{
		if (string.IsNullOrWhiteSpace(transferCode))
		{
			return string.Empty;
		}
		return $"{transferCode.Substring(0, 4)} {transferCode.Substring(4, 4)} {transferCode.Substring(8, 4)} {transferCode.Substring(12, 4)}";
	}
}
