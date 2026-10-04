using System;

namespace SilentNotes.Workers;

public static class UnmixableAlphabet
{
	public static readonly char[] Characters = new char[56]
	{
		'2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B',
		'C', 'D', 'E', 'F', 'G', 'H', 'K', 'L', 'M', 'N',
		'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y',
		'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i',
		'j', 'k', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
		'u', 'v', 'w', 'x', 'y', 'z'
	};

	public static bool IsOfCorrectAlphabet(string code)
	{
		foreach (char letter in code)
		{
			if (!IsOfCorrectAlphabet(letter))
			{
				return false;
			}
		}
		return true;
	}

	public static bool IsOfCorrectAlphabet(char letter)
	{
		return Array.BinarySearch(Characters, letter) >= 0;
	}
}
