using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    public class NotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null values first
            if (value == null)
                return false;

            // If the value is a string, use string.IsNullOrEmpty
            if (value is string stringValue)
                return !string.IsNullOrEmpty(stringValue);

            // If it's a numeric type, check if it's not the default value (0)
            if (value is int intValue)
                return intValue != 0;

            if (value is double doubleValue)
                return doubleValue != 0;

            if (value is float floatValue)
                return floatValue != 0;

            // For any other type, considering it as "not empty" if it has a value
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
