using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SilentNotes.Crypto.KeyDerivation;

public class Argon2Cost
{
	public int MemoryKib { get; set; }

	public int Iterations { get; set; }

	public int Parallelism { get; set; }

	public static bool TryParse(string cost, out Argon2Cost costParameters)
	{
		costParameters = null;
		try
		{
			string[] source = cost.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<string, string> dictionary = source.Select((string costPart) => costPart.Split('=')).ToDictionary((string[] s) => s[0], (string[] s) => s[1]);
			int memoryKib = int.Parse(dictionary["m"], NumberStyles.None);
			int iterations = int.Parse(dictionary["t"], NumberStyles.None);
			int parallelism = int.Parse(dictionary["p"], NumberStyles.None);
			costParameters = new Argon2Cost
			{
				MemoryKib = memoryKib,
				Iterations = iterations,
				Parallelism = parallelism
			};
			return true;
		}
		catch
		{
			return false;
		}
	}

	public string Format()
	{
		return $"m={MemoryKib},t={Iterations},p={Parallelism}";
	}
}
