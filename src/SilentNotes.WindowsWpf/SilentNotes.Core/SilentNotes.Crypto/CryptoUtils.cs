using System;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using SilentNotes.Crypto.KeyDerivation;
using VanillaCloudStorageClient;

namespace SilentNotes.Crypto;

public static class CryptoUtils
{
	private const string CryptorObfuscationPackageName = "obfuscation";

	public static byte[] StringToBytes(string text)
	{
		return Encoding.UTF8.GetBytes(text);
	}

	public static string BytesToString(byte[] bytes)
	{
		return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
	}

	public static byte[] Base64StringToBytes(string text)
	{
		return Convert.FromBase64String(text);
	}

	public static string BytesToBase64String(byte[] bytes)
	{
		return Convert.ToBase64String(bytes);
	}

	public static string StringToBase64String(string text)
	{
		if (text == null)
		{
			return null;
		}
		return BytesToBase64String(StringToBytes(text));
	}

	public static string Base64StringToString(string base64Text)
	{
		if (base64Text == null)
		{
			return null;
		}
		return BytesToString(Base64StringToBytes(base64Text));
	}

	[MethodImpl(MethodImplOptions.NoOptimization)]
	public static void CleanArray<T>(T[] arr)
	{
		if (arr != null)
		{
			Array.Clear(arr, 0, arr.Length);
		}
	}

	public static string GenerateRandomBase62String(int length, ICryptoRandomSource randomSource)
	{
		if (length < 0)
		{
			throw new ArgumentOutOfRangeException("length");
		}
		StringBuilder stringBuilder = new StringBuilder();
		int num = length;
		do
		{
			int numberOfBytes = (int)((double)num * 3.0 / 4.0 + 1.0);
			byte[] randomBytes = randomSource.GetRandomBytes(numberOfBytes);
			string value = Convert.ToBase64String(randomBytes);
			stringBuilder.Append(value);
			stringBuilder.Replace("+", string.Empty);
			stringBuilder.Replace("/", string.Empty);
			stringBuilder.Replace("=", string.Empty);
			num = length - stringBuilder.Length;
		}
		while (num > 0);
		stringBuilder.Length = length;
		return stringBuilder.ToString();
	}

	public static byte[] TruncateKey(byte[] key, int maxLength)
	{
		byte[] array;
		if (key == null || key.Length <= maxLength)
		{
			array = key;
		}
		else
		{
			array = new byte[maxLength];
			Array.Copy(key, 0, array, 0, maxLength);
		}
		return array;
	}

	public static byte[] Obfuscate(byte[] plainMessage, SecureString obfuscationKey, ICryptoRandomSource randomSource)
	{
		ICryptor cryptor = new Cryptor("obfuscation", randomSource);
		return cryptor.Encrypt(plainMessage, obfuscationKey, KeyDerivationCostType.Low, "xchacha20_poly1305", "pbkdf2");
	}

	public static string Obfuscate(string plainText, SecureString obfuscationKey, ICryptoRandomSource randomSource)
	{
		return BytesToBase64String(Obfuscate(StringToBytes(plainText), obfuscationKey, randomSource));
	}

	public static byte[] Deobfuscate(byte[] obfuscatedMessage, SecureString obfuscationKey)
	{
		ICryptor cryptor = new Cryptor("obfuscation", null);
		bool needsReEncryption;
		return cryptor.Decrypt(obfuscatedMessage, obfuscationKey, out needsReEncryption);
	}

	public static string Deobfuscate(string obfuscatedText, SecureString obfuscationKey)
	{
		return BytesToString(Deobfuscate(Base64StringToBytes(obfuscatedText), obfuscationKey));
	}

	public static SecureString StringToSecureString(string password)
	{
		return SecureStringExtensions.StringToSecureString(password);
	}
}
