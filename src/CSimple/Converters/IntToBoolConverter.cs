using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that converts an integer value to boolean based on comparison with a threshold.
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts an integer to a boolean by comparing with the threshold value in parameter.
        /// </summary>
        /// <param name="value">Integer value to evaluate</param>
        /// <param name="targetType">The target type (ignored)</param>
        /// <param name="parameter">Threshold value or comparison mode</param>
        /// <param name="culture">Culture info (ignored)</param>
        /// <returns>Boolean result of the comparison</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Default threshold is 0
            int threshold = 0;

            // If parameter is provided, try to parse it
            if (parameter is string stringParam)
            {
                int.TryParse(stringParam, out threshold);
            }
            else if (parameter is int intParam)
            {
                threshold = intParam;
            }

            // Convert value to int if possible
            if (value is int intValue)
            {
                // Return true if value is greater than threshold
                return intValue > threshold;
            }

            // Try parsing string to int
            if (value is string stringValue && int.TryParse(stringValue, out int parsed))
            {
                return parsed > threshold;
            }

            // Default to false for non-integer values
            return false;
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
