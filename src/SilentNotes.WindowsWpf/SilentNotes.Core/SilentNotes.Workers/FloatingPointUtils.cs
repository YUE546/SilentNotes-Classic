using System.Globalization;

namespace SilentNotes.Workers;

public static class FloatingPointUtils
{
	public static string FormatInvariant(double value, string fmt = "0.###")
	{
		return value.ToString(fmt, CultureInfo.InvariantCulture);
	}
}
