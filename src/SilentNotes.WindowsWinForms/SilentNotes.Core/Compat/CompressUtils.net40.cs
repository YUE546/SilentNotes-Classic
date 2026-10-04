// Compat copy of SilentNotes.AllPlatforms\Workers\CompressUtils.cs for .NET 4.0.
// Differences to the frozen original:
// - GZip uses the CompressionMode overload (CompressionLevel is .NET 4.5+), the produced
//   gzip streams are equivalent.
// - ZipArchive (.NET 4.5+) is replaced by a hand written zip reader/writer. The writer
//   creates store-mode entries (valid standard zip), the reader understands both store and
//   deflate entries, so backups created by older .NET 4.7.2 builds remain readable.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SilentNotes.Workers
{
    /// <summary>
    /// Helper class for compression and decompression of data with the GZip algorithm.
    /// </summary>
    public static class CompressUtils
    {
        /// <summary>
        /// Compresses data with GZip. Don't use this function repeatedly on already compressed data.
        /// </summary>
        /// <param name="data">Data to compress.</param>
        /// <returns>Compressed data, or null if <paramref name="data"/> was null.</returns>
        public static byte[] Compress(byte[] data)
        {
            if (data == null)
                return null;
            if (data.Length == 0)
                return new byte[0];

            byte[] result;
            using (MemoryStream inputStream = new MemoryStream(data))
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (GZipStream zipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    inputStream.CopyTo(zipStream);
                }
                result = outputStream.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Decompresses data previously compressed with <see cref="Compress(byte[])"/>.
        /// </summary>
        /// <param name="compressedData">Compressed data to decompress.</param>
        /// <returns>Decompressed data, or null if <paramref name="compressedData"/> was null.</returns>
        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null)
                return null;
            if (compressedData.Length == 0)
                return new byte[0];

            byte[] result;
            using (MemoryStream inputStream = new MemoryStream(compressedData))
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (GZipStream zipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    zipStream.CopyTo(outputStream);
                }
                result = outputStream.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Creates a zip archive from a list of files.
        /// Be aware that this function does not support sub directories.
        /// </summary>
        /// <param name="entries">List of files, containing the filename and the file content.</param>
        /// <returns>The content of the zip archive.</returns>
        public static byte[] CreateZipArchive(IEnumerable<CompressEntry> entries)
        {
            byte[] result;
            using (MemoryStream outputStream = new MemoryStream())
            {
                List<byte[]> centralDirectory = new List<byte[]>();
                foreach (CompressEntry inputEntry in entries)
                {
                    uint crc32 = Crc32Of(inputEntry.Data);
                    byte[] nameBytes = Encoding.UTF8.GetBytes(inputEntry.Name);
                    int localOffset = (int)outputStream.Length;

                    // Local file header, sizes are known upfront, no data descriptor needed.
                    WriteUInt32LittleEndian(outputStream, 0x04034b50);
                    WriteUInt16LittleEndian(outputStream, 20);        // version needed
                    WriteUInt16LittleEndian(outputStream, 0);         // flags
                    WriteUInt16LittleEndian(outputStream, 0);         // method: store
                    WriteUInt16LittleEndian(outputStream, 0);         // modified time
                    WriteUInt16LittleEndian(outputStream, 0);         // modified date
                    WriteUInt32LittleEndian(outputStream, crc32);
                    WriteUInt32LittleEndian(outputStream, (uint)inputEntry.Data.Length);
                    WriteUInt32LittleEndian(outputStream, (uint)inputEntry.Data.Length);
                    WriteUInt16LittleEndian(outputStream, (ushort)nameBytes.Length);
                    WriteUInt16LittleEndian(outputStream, 0);         // extra length
                    outputStream.Write(nameBytes, 0, nameBytes.Length);
                    outputStream.Write(inputEntry.Data, 0, inputEntry.Data.Length);

                    // Central directory entry for this file
                    MemoryStream centralEntry = new MemoryStream();
                    WriteUInt32LittleEndian(centralEntry, 0x02014b50);
                    WriteUInt16LittleEndian(centralEntry, 20);        // version made by
                    WriteUInt16LittleEndian(centralEntry, 20);        // version needed
                    WriteUInt16LittleEndian(centralEntry, 0);         // flags
                    WriteUInt16LittleEndian(centralEntry, 0);         // method: store
                    WriteUInt16LittleEndian(centralEntry, 0);         // modified time
                    WriteUInt16LittleEndian(centralEntry, 0);         // modified date
                    WriteUInt32LittleEndian(centralEntry, crc32);
                    WriteUInt32LittleEndian(centralEntry, (uint)inputEntry.Data.Length);
                    WriteUInt32LittleEndian(centralEntry, (uint)inputEntry.Data.Length);
                    WriteUInt16LittleEndian(centralEntry, (ushort)nameBytes.Length);
                    WriteUInt16LittleEndian(centralEntry, 0);         // extra length
                    WriteUInt16LittleEndian(centralEntry, 0);         // comment length
                    WriteUInt16LittleEndian(centralEntry, 0);         // disk number start
                    WriteUInt16LittleEndian(centralEntry, 0);         // internal attributes
                    WriteUInt32LittleEndian(centralEntry, 0);         // external attributes
                    WriteUInt32LittleEndian(centralEntry, (uint)localOffset);
                    centralEntry.Write(nameBytes, 0, nameBytes.Length);
                    centralDirectory.Add(centralEntry.ToArray());
                }

                uint centralDirectoryOffset = (uint)outputStream.Length;
                foreach (byte[] centralEntry in centralDirectory)
                    outputStream.Write(centralEntry, 0, centralEntry.Length);
                uint centralDirectorySize = (uint)outputStream.Length - centralDirectoryOffset;

                // End of central directory record
                WriteUInt32LittleEndian(outputStream, 0x06054b50);
                WriteUInt16LittleEndian(outputStream, 0);             // disk number
                WriteUInt16LittleEndian(outputStream, 0);             // disk with central dir
                WriteUInt16LittleEndian(outputStream, (ushort)centralDirectory.Count);
                WriteUInt16LittleEndian(outputStream, (ushort)centralDirectory.Count);
                WriteUInt32LittleEndian(outputStream, centralDirectorySize);
                WriteUInt32LittleEndian(outputStream, centralDirectoryOffset);
                WriteUInt16LittleEndian(outputStream, 0);             // comment length

                result = outputStream.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Opens a zip archive and returns a list of files.
        /// </summary>
        /// <param name="zipContent">The content of a zip archive.</param>
        /// <returns>List of files, containing the filename and the file content.</returns>
        public static List<CompressEntry> OpenZipArchive(byte[] zipContent)
        {
            List<CompressEntry> result = new List<CompressEntry>();
            int endOfCentralDirectoryIndex = FindEndOfCentralDirectory(zipContent);
            if (endOfCentralDirectoryIndex < 0)
                throw new InvalidDataException("No zip archive found");

            ushort entryCount = ReadUInt16LittleEndian(zipContent, endOfCentralDirectoryIndex + 10);
            uint centralDirectoryOffset = ReadUInt32LittleEndian(zipContent, endOfCentralDirectoryIndex + 16);

            int centralIndex = (int)centralDirectoryOffset;
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                const int FixedCentralHeaderSize = 46;
                if (ReadUInt32LittleEndian(zipContent, centralIndex) != 0x02014b50)
                    throw new InvalidDataException("Unexpected zip structure");

                ushort method = ReadUInt16LittleEndian(zipContent, centralIndex + 10);
                uint compressedSize = ReadUInt32LittleEndian(zipContent, centralIndex + 20);
                ushort nameLength = ReadUInt16LittleEndian(zipContent, centralIndex + 28);
                ushort extraLength = ReadUInt16LittleEndian(zipContent, centralIndex + 30);
                ushort commentLength = ReadUInt16LittleEndian(zipContent, centralIndex + 32);
                uint localHeaderOffset = ReadUInt32LittleEndian(zipContent, centralIndex + 42);
                string name = Encoding.UTF8.GetString(zipContent, centralIndex + FixedCentralHeaderSize, nameLength);

                // Skip the local header (its sizes may be zero when a data descriptor is used,
                // therefore the sizes of the central directory are used).
                const int FixedLocalHeaderSize = 30;
                if (ReadUInt32LittleEndian(zipContent, (int)localHeaderOffset) != 0x04034b50)
                    throw new InvalidDataException("Unexpected zip structure");
                ushort localNameLength = ReadUInt16LittleEndian(zipContent, (int)localHeaderOffset + 26);
                ushort localExtraLength = ReadUInt16LittleEndian(zipContent, (int)localHeaderOffset + 28);
                int dataOffset = (int)localHeaderOffset + FixedLocalHeaderSize + localNameLength + localExtraLength;

                byte[] compressedData = new byte[compressedSize];
                Array.Copy(zipContent, dataOffset, compressedData, 0, compressedSize);

                byte[] uncompressedData;
                if (method == 0)
                {
                    // Stored entry, data is taken as it is.
                    uncompressedData = compressedData;
                }
                else if (method == 8)
                {
                    // Deflate entry (raw deflate stream, as also used by ZipArchive and old builds).
                    using (MemoryStream compressedStream = new MemoryStream(compressedData))
                    using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                    using (MemoryStream uncompressedStream = new MemoryStream())
                    {
                        deflateStream.CopyTo(uncompressedStream);
                        uncompressedData = uncompressedStream.ToArray();
                    }
                }
                else
                {
                    throw new InvalidDataException("Unsupported zip compression method");
                }

                result.Add(new CompressEntry
                {
                    Name = name,
                    Data = uncompressedData
                });
                centralIndex += FixedCentralHeaderSize + nameLength + extraLength + commentLength;
            }
            return result;
        }

        private static int FindEndOfCentralDirectory(byte[] zipContent)
        {
            const int MinimalEocdSize = 22;
            int scanEnd = Math.Max(0, zipContent.Length - MinimalEocdSize - 65535);
            for (int index = zipContent.Length - MinimalEocdSize; index >= scanEnd; index--)
            {
                if ((zipContent[index] == 0x50) && (zipContent[index + 1] == 0x4b) &&
                    (zipContent[index + 2] == 0x05) && (zipContent[index + 3] == 0x06))
                {
                    return index;
                }
            }
            return -1;
        }

        private static uint Crc32Of(byte[] data)
        {
            // Classic CRC32 with polynomial 0xEDB88320 (as used by the zip format).
            uint[] crcTable = new uint[256];
            for (uint index = 0; index < 256; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++)
                    value = ((value & 1) != 0) ? (0xEDB88320 ^ (value >> 1)) : (value >> 1);
                crcTable[index] = value;
            }

            uint crc = 0xFFFFFFFF;
            for (int index = 0; index < data.Length; index++)
                crc = crcTable[(crc ^ data[index]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        private static void WriteUInt16LittleEndian(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
        }

        private static void WriteUInt32LittleEndian(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static ushort ReadUInt16LittleEndian(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset]) | (buffer[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] buffer, int offset)
        {
            return (uint)((buffer[offset]) | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24));
        }

        public class CompressEntry
        {
            public string Name { get; set; }

            public byte[] Data { get; set; }
        }
    }
}
