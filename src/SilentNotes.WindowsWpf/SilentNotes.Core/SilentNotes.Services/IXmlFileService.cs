using System.Xml.Linq;

namespace SilentNotes.Services;

public interface IXmlFileService
{
	bool TryLoad(string filePath, out XDocument xml);

	bool TrySerializeAndSave(string filePath, object serializeableObject);

	bool Exists(string filePath);
}
