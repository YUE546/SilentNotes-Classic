using System.Xml.Serialization;

namespace SilentNotes.Models;

public class ColorThemeModel
{
	public class ColorSetModel
	{
		[XmlAttribute(AttributeName = "primary")]
		public string Primary { get; set; }

		[XmlAttribute(AttributeName = "secondary")]
		public string Secondary { get; set; }

		[XmlAttribute(AttributeName = "decoration")]
		public string Decoration { get; set; }

		[XmlAttribute(AttributeName = "app_bar")]
		public string AppBar { get; set; }

		[XmlAttribute(AttributeName = "app_bar_toggled")]
		public string AppBarToggled { get; set; }

		[XmlAttribute(AttributeName = "app_bar_secondary")]
		public string AppBarSecondary { get; set; }
	}

	private ColorSetModel _lightColors;

	private ColorSetModel _darkColors;

	[XmlAttribute(AttributeName = "name")]
	public string Name { get; set; }

	[XmlAttribute(AttributeName = "light_colors")]
	public ColorSetModel LightColors
	{
		get
		{
			return _lightColors ?? (_lightColors = new ColorSetModel());
		}
		set
		{
			_lightColors = value;
		}
	}

	[XmlAttribute(AttributeName = "dark_colors")]
	public ColorSetModel DarkColors
	{
		get
		{
			return _darkColors ?? (_darkColors = new ColorSetModel());
		}
		set
		{
			_darkColors = value;
		}
	}
}
