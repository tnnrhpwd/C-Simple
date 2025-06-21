using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that changes color based on integer value comparison.
    /// </summary>
    public class IntToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts an integer value to a color based on a threshold comparison.
        /// Parameter format should be: "threshold|colorIfLessThanOrEqual|colorIfGreaterThan"
        /// </summary>
        /// <param name="value">Integer value to evaluate</param>
        /// <param name="targetType">The target type (ignored)</param>
        /// <param name="parameter">Format string with threshold and colors</param>
        /// <param name="culture">Culture info (ignored)</param>
        /// <returns>Color object based on the comparison</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int intValue))
                return Colors.Gray;

            if (parameter is string param)
            {
                string[] parts = param.Split('|');
                if (parts.Length >= 3 && int.TryParse(parts[0], out int threshold))
                {
                    string colorName = intValue <= threshold ? parts[1] : parts[2];
                    return ParseColor(colorName);
                }
            }

            // Default return
            return Colors.Gray;
        }

        /// <summary>
        /// Parses a color name or hex code to a Color object
        /// </summary>
        private Color ParseColor(string colorName)
        {
            // Try to parse as a named color using reflection
            var colorProps = typeof(Colors).GetProperties();
            foreach (var prop in colorProps)
            {
                if (string.Equals(prop.Name, colorName, StringComparison.OrdinalIgnoreCase))
                    return (Color)prop.GetValue(null);
            }

            // Try to parse as a hex code
            if (colorName.StartsWith("#"))
            {
                try
                {
                    return Color.FromArgb(colorName);
                }
                catch
                {
                    // If parsing fails, return a default color
                    return Colors.Gray;
                }
            }

            // Default color
            return Colors.Gray;
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
