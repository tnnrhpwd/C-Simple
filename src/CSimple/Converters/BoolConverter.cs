using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that normalizes various values to boolean.
    /// </summary>
    public class BoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value to a boolean.
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="targetType">The type to convert to (ignored)</param>
        /// <param name="parameter">Optional parameter to invert the result</param>
        /// <param name="culture">The culture to use (ignored)</param>
        /// <returns>Boolean representation of the value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = false;

            if (value == null)
            {
                result = false;
            }
            else if (value is bool boolValue)
            {
                result = boolValue;
            }
            else if (value is string stringValue)
            {
                // Try parse as boolean
                if (bool.TryParse(stringValue, out bool parsedBool))
                {
                    result = parsedBool;
                }
                // If not parsable as boolean, check if it's not empty
                else
                {
                    result = !string.IsNullOrEmpty(stringValue);
                }
            }
            else if (value is int intValue)
            {
                result = intValue != 0;
            }
            else
            {
                // For any other non-null value, consider it true
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Converts a boolean back to the original type.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (targetType == typeof(bool))
                {
                    return boolValue;
                }
                else if (targetType == typeof(string))
                {
                    return boolValue.ToString();
                }
                else if (targetType == typeof(int))
                {
                    return boolValue ? 1 : 0;
                }
            }

            return null;
        }
    }

}