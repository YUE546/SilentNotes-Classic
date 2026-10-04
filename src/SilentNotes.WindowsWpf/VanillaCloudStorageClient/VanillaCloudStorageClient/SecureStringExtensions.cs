using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace VanillaCloudStorageClient;

public static class SecureStringExtensions
{
	public static SecureString StringToSecureString(string password)
	{
		if (password == null)
		{
			return null;
		}
		return new NetworkCredential(null, password).SecurePassword;
	}

	public static string SecureStringToString(this SecureString password)
	{
		if (password == null)
		{
			return null;
		}
		return new NetworkCredential(null, password).Password;
	}

	public static bool AreEqual(this SecureString password1, SecureString password2)
	{
		if (password1 == null && password2 == null)
		{
			return true;
		}
		if (password1 == null || password2 == null || password1.Length != password2.Length)
		{
			return false;
		}
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		try
		{
			intPtr = Marshal.SecureStringToBSTR(password1);
			intPtr2 = Marshal.SecureStringToBSTR(password2);
			return BstrAreEqual(intPtr, intPtr2);
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.ZeroFreeBSTR(intPtr);
			}
			if (intPtr2 != IntPtr.Zero)
			{
				Marshal.ZeroFreeBSTR(intPtr2);
			}
		}
	}

	private static bool BstrAreEqual(IntPtr leftBstr, IntPtr rightBstr)
	{
		if (leftBstr == IntPtr.Zero || rightBstr == IntPtr.Zero)
		{
			return false;
		}
		int num = Marshal.ReadInt32(leftBstr, -4);
		int num2 = Marshal.ReadInt32(rightBstr, -4);
		if (num != num2)
		{
			return false;
		}
		bool result = true;
		for (int i = 0; i < num; i++)
		{
			byte b = Marshal.ReadByte(leftBstr + i);
			byte b2 = Marshal.ReadByte(rightBstr + i);
			if (b != b2)
			{
				result = false;
			}
		}
		return result;
	}

	public static byte[] SecureStringToBytes(this SecureString secretString, Encoding encoding)
	{
		if (encoding == null)
		{
			encoding = Encoding.UTF8;
		}
		if (secretString == null)
		{
			return null;
		}
		if (secretString.Length == 0)
		{
			return new byte[0];
		}
		IntPtr intPtr = IntPtr.Zero;
		char[] array = null;
		try
		{
			intPtr = Marshal.SecureStringToBSTR(secretString);
			array = new char[secretString.Length];
			Marshal.Copy(intPtr, array, 0, array.Length);
			return encoding.GetBytes(array);
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.ZeroFreeBSTR(intPtr);
			}
			if (array != null)
			{
				Array.Clear(array, 0, array.Length);
			}
		}
	}

	public static SecureString BytesToSecureString(byte[] secretBytes, Encoding encoding)
	{
		if (encoding == null)
		{
			encoding = Encoding.UTF8;
		}
		if (secretBytes == null)
		{
			return null;
		}
		char[] array = null;
		try
		{
			SecureString secureString = new SecureString();
			array = encoding.GetChars(secretBytes);
			char[] array2 = array;
			foreach (char c in array2)
			{
				secureString.AppendChar(c);
			}
			return secureString;
		}
		finally
		{
			if (array != null)
			{
				Array.Clear(array, 0, array.Length);
			}
		}
	}
}
