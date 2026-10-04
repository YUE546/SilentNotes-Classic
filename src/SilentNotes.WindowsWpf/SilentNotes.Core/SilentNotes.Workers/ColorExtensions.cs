using System.Drawing;
using System.Globalization;

namespace SilentNotes.Workers;

public static class ColorExtensions
{
	public static bool IsDark(this Color color)
	{
		float brightness = color.GetBrightness();
		return (double)brightness < 0.5;
	}

	public static Color HexToColor(string colorHex)
	{
		colorHex = colorHex.Replace("#", string.Empty);
		if (colorHex.Length <= 6)
		{
			colorHex = "ff" + colorHex;
		}
		int argb = int.Parse(colorHex, NumberStyles.HexNumber);
		return Color.FromArgb(argb);
	}
}
