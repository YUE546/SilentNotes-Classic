using System;
using System.Collections.Generic;
using System.Linq;

namespace SilentNotes.Models;

public static class ColorThemeExtensions
{
	public static ColorThemeModel FindByNameOrDefault(this IEnumerable<ColorThemeModel> items, string themeName)
	{
		ColorThemeModel colorThemeModel = items.FirstOrDefault();
		return items.FirstOrDefault((ColorThemeModel item) => string.Equals(item.Name, themeName, StringComparison.InvariantCultureIgnoreCase)) ?? colorThemeModel;
	}
}
