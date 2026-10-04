using System;
using System.Buffers.Binary;

namespace SilentNotes.Workers;

public static class RelativeGuid
{
	public static Guid CreateRelativeGuid(Guid originalGuid, int distance)
	{
		byte[] array = originalGuid.ToByteArray();
		System.Span<byte> span = MemoryExtensions.AsSpan<byte>(array).Slice(12, 4);
		uint num = BinaryPrimitives.ReadUInt32BigEndian((span));
		num += (uint)distance;
		BinaryPrimitives.WriteUInt32BigEndian(span, num);
		return new Guid(array);
	}

	public static bool AreGuidsRelated(Guid guid1, Guid guid2)
	{
		System.Span<byte> guidBytes = (guid1.ToByteArray());
		System.Span<byte> guidBytes2 = (guid2.ToByteArray());
		return AreGuidsRelated(guidBytes, guidBytes2);
	}

	private static bool AreGuidsRelated(System.Span<byte> guidBytes1, System.Span<byte> guidBytes2)
	{
		System.Span<byte> span = guidBytes1.Slice(0, 12);
		System.Span<byte> span2 = guidBytes2.Slice(0, 12);
		return MemoryExtensions.SequenceEqual<byte>(span, (span2));
	}

	public static int CompareRelativeGuids(Guid relativeGuid, Guid originalGuid)
	{
		System.Span<byte> relativeBytes = (relativeGuid.ToByteArray());
		System.Span<byte> originalBytes = (originalGuid.ToByteArray());
		return CompareRelativeGuids(relativeBytes, originalBytes);
	}

	private static int CompareRelativeGuids(System.Span<byte> relativeBytes, System.Span<byte> originalBytes)
	{
		uint num = BinaryPrimitives.ReadUInt32BigEndian((relativeBytes.Slice(12, 4)));
		uint num2 = BinaryPrimitives.ReadUInt32BigEndian((originalBytes.Slice(12, 4)));
		return (int)(num - num2);
	}
}

