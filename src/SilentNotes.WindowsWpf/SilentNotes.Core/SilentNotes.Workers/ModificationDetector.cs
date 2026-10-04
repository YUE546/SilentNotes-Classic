using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SilentNotes.Workers;

internal class ModificationDetector
{
	private readonly Func<long?> _fingerPrintProvider;

	private long? _memorizedFingerPrint;

	public ModificationDetector(Func<long?> fingerPrintProvider, bool memorizeCurrentState = true)
	{
		if (fingerPrintProvider == null)
		{
			throw new ArgumentNullException("fingerPrintProvider");
		}
		_fingerPrintProvider = fingerPrintProvider;
		if (memorizeCurrentState)
		{
			MemorizeCurrentState();
		}
	}

	public virtual void MemorizeCurrentState()
	{
		_memorizedFingerPrint = _fingerPrintProvider();
	}

	public bool IsModified()
	{
		long? num = _fingerPrintProvider();
		return _memorizedFingerPrint != num;
	}

	public static long CombineHashCodes(IEnumerable<long> hashCodes, long withOldHashCode = 0L)
	{
		long num = withOldHashCode;
		foreach (long hashCode in hashCodes)
		{
			num = (num * 397) ^ hashCode;
		}
		return num;
	}

	public static long CombineWithStringHash(string text, long withOldHashCode = 0L)
	{
		if (text == null)
		{
			return withOldHashCode;
		}
		System.Span<byte> span;
		using (SHA256 sHA = SHA256.Create())
		{
			span = (sHA.ComputeHash(Encoding.UTF8.GetBytes(text)));
		}
		long[] array = new long[4];
		for (int i = 0; i < 4; i++)
		{
			System.Span<byte> span2 = span.Slice(i * 8, 8);
			array[i] = BinaryPrimitives.ReadInt64BigEndian((span2));
		}
		return CombineHashCodes(array, withOldHashCode);
	}
}

