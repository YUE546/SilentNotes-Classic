using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SilentNotes.Workers;

public static class CompressUtils
{
	public class CompressEntry
	{
		public string Name { get; set; }

		public byte[] Data { get; set; }
	}

	public static byte[] Compress(byte[] data)
	{
		if (data == null)
		{
			return null;
		}
		if (data.Length == 0)
		{
			return new byte[0];
		}
		byte[] result;
		using (MemoryStream memoryStream = new MemoryStream(data))
		{
			using MemoryStream memoryStream2 = new MemoryStream();
			using (GZipStream destination = new GZipStream(memoryStream2, CompressionLevel.Optimal))
			{
				memoryStream.CopyTo(destination);
			}
			result = memoryStream2.ToArray();
		}
		return result;
	}

	public static byte[] Decompress(byte[] compressedData)
	{
		if (compressedData == null)
		{
			return null;
		}
		if (compressedData.Length == 0)
		{
			return new byte[0];
		}
		byte[] result;
		using (MemoryStream stream = new MemoryStream(compressedData))
		{
			using MemoryStream memoryStream = new MemoryStream();
			using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
			{
				gZipStream.CopyTo(memoryStream);
			}
			result = memoryStream.ToArray();
		}
		return result;
	}

	public static byte[] CreateZipArchive(IEnumerable<CompressEntry> entries)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		using MemoryStream memoryStream = new MemoryStream();
		ZipArchive val = new ZipArchive((Stream)memoryStream, (ZipArchiveMode)1, true);
		try
		{
			foreach (CompressEntry entry in entries)
			{
				ZipArchiveEntry val2 = val.CreateEntry(entry.Name);
				using Stream stream = val2.Open();
				stream.Write(entry.Data, 0, entry.Data.Length);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		return memoryStream.ToArray();
	}

	public static List<CompressEntry> OpenZipArchive(byte[] zipContent)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		List<CompressEntry> list = new List<CompressEntry>();
		using (MemoryStream memoryStream = new MemoryStream(zipContent))
		{
			ZipArchive val = new ZipArchive((Stream)memoryStream, (ZipArchiveMode)0);
			try
			{
				foreach (ZipArchiveEntry entry in val.Entries)
				{
					using Stream stream = entry.Open();
					using MemoryStream memoryStream2 = new MemoryStream();
					stream.CopyTo(memoryStream2);
					list.Add(new CompressEntry
					{
						Name = entry.Name,
						Data = memoryStream2.ToArray()
					});
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		return list;
	}
}
