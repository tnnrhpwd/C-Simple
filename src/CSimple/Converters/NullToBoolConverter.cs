using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that converts a null or empty value to a boolean.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value to a boolean indicating if the value is not null/empty.
        /// </summary>
        /// <param name="value">The value to check</param>
        /// <param name="targetType">The type to convert to (ignored)</param>
        /// <param name="parameter">Optional parameter to invert the result</param>
        /// <param name="culture">The culture to use (ignored)</param>
        /// <returns>Boolean value: true if not null/empty, false otherwise (or inverted if parameter is provided)</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result;

            if (value == null)
            {
                result = false;
            }
            else if (value is string stringValue)
            {
                result = !string.IsNullOrEmpty(stringValue);
            }
            else
            {
                // If it's not null and not a string, consider it as "has value"
                result = true;
            }

            // If parameter is provided, invert the result
            if (parameter != null)
            {
                result = !result;
            }

            return result;
        }

        /// <summary>
        /// Not implemented - conversion back is not supported
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
