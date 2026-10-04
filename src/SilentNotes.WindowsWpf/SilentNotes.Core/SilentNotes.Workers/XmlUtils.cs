using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SilentNotes.Workers;

public class XmlUtils
{
	public static void SerializeToXmlStream(object obj, Stream outputStream, Encoding encoding = null)
	{
		if (encoding == null)
		{
			encoding = Encoding.UTF8;
		}
		XmlWriterSettings settings = new XmlWriterSettings
		{
			CheckCharacters = false,
			Indent = true,
			Encoding = encoding
		};
		using XmlWriter xmlWriter = XmlWriter.Create(outputStream, settings);
		XmlSerializer xmlSerializer = new XmlSerializer(obj.GetType());
		xmlSerializer.Serialize(xmlWriter, obj);
	}

	public static byte[] SerializeToXmlBytes(object obj, Encoding encoding = null)
	{
		using MemoryStream memoryStream = new MemoryStream();
		SerializeToXmlStream(obj, memoryStream, encoding);
		return memoryStream.ToArray();
	}

	internal static XDocument SerializeToXmlDocument(object obj)
	{
		XDocument xDocument = new XDocument();
		using (XmlWriter xmlWriter = xDocument.CreateWriter())
		{
			XmlSerializer xmlSerializer = new XmlSerializer(obj.GetType());
			xmlSerializer.Serialize(xmlWriter, obj);
		}
		return xDocument;
	}

	internal static string SerializeToString(object obj)
	{
		Encoding unicode = Encoding.Unicode;
		byte[] bytes = SerializeToXmlBytes(obj, unicode);
		return unicode.GetString(bytes);
	}

	public static XDocument LoadFromXmlBytes(byte[] bytes)
	{
		using MemoryStream stream = new MemoryStream(bytes);
		return XDocument.Load(stream);
	}

	public static T DeserializeFromXmlDocument<T>(XDocument xml)
	{
		using XmlReader xmlReader = xml.CreateReader();
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
		return (T)xmlSerializer.Deserialize(xmlReader);
	}

	public static string SanitizeXmlString(string xml)
	{
		if (xml == null)
		{
			return null;
		}
		Encoding encoding = Encoding.GetEncoding("utf-8", new EncoderReplacementFallback(string.Empty), new DecoderReplacementFallback(string.Empty));
		byte[] bytes = encoding.GetBytes(xml);
		string text = encoding.GetString(bytes);
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (IsAcceptedXmlCharacter(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static bool IsAcceptedXmlCharacter(char letter)
	{
		return (letter >= ' ' && letter < '\ufffe') || letter > '\uffff' || letter == '\t' || letter == '\n' || letter == '\r';
	}

	private static bool IsLegalXmlChar(int character)
	{
		return character == 9 || character == 10 || character == 13 || (character >= 32 && character <= 55295) || (character >= 57344 && character <= 65533) || (character >= 65536 && character <= 1114111);
	}
}
