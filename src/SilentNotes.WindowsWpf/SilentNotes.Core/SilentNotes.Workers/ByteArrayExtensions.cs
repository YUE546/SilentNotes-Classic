using System;

namespace SilentNotes.Workers;

public static class ByteArrayExtensions
{
	public static bool ContainsAt(this byte[] haystack, byte[] needle, int index)
	{
		if (haystack == null)
		{
			throw new ArgumentNullException("haystack");
		}
		if (needle == null)
		{
			throw new ArgumentNullException("needle");
		}
		if (index + needle.Length > haystack.Length || index < 0)
		{
			return false;
		}
		for (int i = 0; i < needle.Length; i++)
		{
			if (haystack[index + i] != needle[i])
			{
				return false;
			}
		}
		return true;
	}

	public static bool ContainsAt(this byte[] haystack, byte needle, int index)
	{
		return haystack.ContainsAt(new byte[1] { needle }, index);
	}

	public static bool ContainsDigitCharAt(this byte[] array, int index)
	{
		if (array == null)
		{
			throw new ArgumentNullException("array");
		}
		if (index >= array.Length || index < 0)
		{
			return false;
		}
		byte b = array[index];
		return b >= 48 && b <= 57;
	}
}
