using System;
using System.Text;
using SilentNotes.Workers;

namespace SilentNotes.Crypto;

public class CryptoHeaderPacker
{
	private const char Separator = '$';

	private const string RevisionSeparator = " v=";

	public static byte[] PackHeaderAndCypher(CryptoHeader header, byte[] cipher)
	{
		if (header == null)
		{
			throw new ArgumentNullException("header");
		}
		if (cipher == null)
		{
			throw new ArgumentNullException("cipher");
		}
		if (!string.IsNullOrEmpty(header.Cost) && header.Cost.Contains('$'.ToString()))
		{
			throw new ArgumentException("The cost parameter must not contain a separator character.");
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(header.PackageName + " v=" + 2);
		stringBuilder.Append('$');
		stringBuilder.Append(header.AlgorithmName);
		stringBuilder.Append('$');
		stringBuilder.Append(CryptoUtils.BytesToBase64String(header.Nonce));
		stringBuilder.Append('$');
		stringBuilder.Append(header.KdfName);
		stringBuilder.Append('$');
		if (header.Salt != null)
		{
			stringBuilder.Append(CryptoUtils.BytesToBase64String(header.Salt));
		}
		stringBuilder.Append('$');
		stringBuilder.Append(header.Cost);
		stringBuilder.Append('$');
		stringBuilder.Append(header.Compression);
		stringBuilder.Append('$');
		string text = stringBuilder.ToString();
		byte[] array = CryptoUtils.StringToBytes(text);
		byte[] array2 = new byte[cipher.Length + array.Length];
		Array.Copy(array, 0, array2, 0, array.Length);
		Array.Copy(cipher, 0, array2, array.Length, cipher.Length);
		return array2;
	}

	public static bool HasMatchingHeader(byte[] packedCipher, string expectedAppName, out int revision)
	{
		if (packedCipher != null && packedCipher.ContainsAt(CryptoUtils.StringToBytes(expectedAppName), 0))
		{
			int length = expectedAppName.Length;
			if (packedCipher.ContainsAt(36, length))
			{
				revision = 1;
				return true;
			}
			if (packedCipher.ContainsAt(CryptoUtils.StringToBytes(" v="), length))
			{
				int num = length + " v=".Length;
				int i;
				for (i = num; packedCipher.ContainsDigitCharAt(i); i++)
				{
				}
				if (i > num && packedCipher.ContainsAt(36, i))
				{
					byte[] array = new byte[i - num];
					Array.Copy(packedCipher, num, array, 0, array.Length);
					revision = int.Parse(CryptoUtils.BytesToString(array));
					return true;
				}
			}
		}
		revision = 0;
		return false;
	}

	public static void UnpackHeaderAndCipher(byte[] packedCipher, string expectedAppName, out CryptoHeader header, out byte[] cipher)
	{
		header = null;
		cipher = null;
		if (!HasMatchingHeader(packedCipher, expectedAppName, out var revision))
		{
			throw new CryptoExceptionInvalidCipherFormat();
		}
		if (revision > 2)
		{
			throw new CryptoUnsupportedRevisionException();
		}
		int expectedSeparatorCount = ((revision > 1) ? 7 : 6);
		int num = IndexOfLastSeparator(packedCipher, expectedSeparatorCount);
		if (num < 0)
		{
			throw new CryptoExceptionInvalidCipherFormat();
		}
		int num2 = packedCipher.Length - num - 1;
		cipher = new byte[num2];
		Array.Copy(packedCipher, num + 1, cipher, 0, num2);
		try
		{
			string text = Encoding.UTF8.GetString(packedCipher, 0, num + 1);
			string[] array = text.Split('$');
			header = new CryptoHeader
			{
				PackageName = expectedAppName,
				Revision = revision,
				AlgorithmName = array[1],
				Nonce = CryptoUtils.Base64StringToBytes(array[2]),
				KdfName = (string.IsNullOrEmpty(array[3]) ? null : array[3]),
				Salt = (string.IsNullOrEmpty(array[4]) ? null : CryptoUtils.Base64StringToBytes(array[4])),
				Cost = (string.IsNullOrEmpty(array[5]) ? null : array[5])
			};
			if (revision > 1)
			{
				header.Compression = array[6];
			}
		}
		catch (Exception)
		{
			throw new CryptoExceptionInvalidCipherFormat();
		}
	}

	private static int IndexOfLastSeparator(byte[] packedCipher, int expectedSeparatorCount)
	{
		byte b = Convert.ToByte('$');
		int num = 0;
		for (int i = 0; i < packedCipher.Length; i++)
		{
			if (b == packedCipher[i])
			{
				num++;
			}
			if (num == expectedSeparatorCount)
			{
				return i;
			}
		}
		return -1;
	}
}
