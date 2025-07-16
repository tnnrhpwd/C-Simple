using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter to convert integer percentage (0-100) to progress value (0.0-1.0)
    /// </summary>
    public class IntToProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int percentage)
            {
                return Math.Max(0.0, Math.Min(1.0, percentage / 100.0));
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                return (int)(progress * 100);
            }
            return 0;
        }
    }
}
