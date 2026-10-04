using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SilentNotes.WindowsWpf.Converters
{
    /// <summary>
    /// Converts null/empty strings to Collapsed visibility, non-null to Visible.
    /// </summary>
    internal class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (string.IsNullOrWhiteSpace(str))
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
