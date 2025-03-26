using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    public class BoolToStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "Active" : "Inactive";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToLower() == "active";
        }
    }
}
