using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using SilentNotes.Workers;

namespace SilentNotes.Services;

public class XmlFileService : IXmlFileService
{
	public bool TryLoad(string filePath, out XDocument xml)
	{
		try
		{
			xml = XDocument.Load(filePath);
			return true;
		}
		catch (Exception)
		{
			xml = null;
			return false;
		}
	}

	public bool TrySerializeAndSave(string filePath, object serializeableObject)
	{
		try
		{
			AtomicFileWriter atomicFileWriter = new AtomicFileWriter
			{
				MinValidFileSize = "</silentnotes>".Length
			};
			atomicFileWriter.Write(filePath, delegate(FileStream xmlStream)
			{
				XmlUtils.SerializeToXmlStream(serializeableObject, xmlStream, Encoding.UTF8);
			});
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool Exists(string filePath)
	{
		AtomicFileWriter atomicFileWriter = new AtomicFileWriter
		{
			MinValidFileSize = "</silentnotes>".Length
		};
		atomicFileWriter.CompletePendingWrite(filePath);
		return File.Exists(filePath);
	}
}
