using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that formats a string using another string as a format parameter.
    /// It can either use StringFormat syntax or replace a placeholder with the value.
    /// </summary>
    public class StringFormatConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value to a formatted string using the parameter as format or replacement.
        /// </summary>
        /// <param name="value">The value to format</param>
        /// <param name="targetType">The type to convert to (ignored)</param>
        /// <param name="parameter">Format string or default value</param>
        /// <param name="culture">The culture to use for formatting</param>
        /// <returns>Formatted string</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If value is null or empty, return parameter as a default value
            if (value == null || (value is string stringValue && string.IsNullOrEmpty(stringValue)))
            {
                return parameter?.ToString() ?? string.Empty;
            }

            // If parameter is a format string with placeholders
            if (parameter is string format && format.Contains("{0}"))
            {
                return string.Format(culture, format, value);
            }

            // Otherwise, just return the value as string
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Not implemented - conversion back is not supported
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not implemented for StringFormatConverter");
        }
    }
}