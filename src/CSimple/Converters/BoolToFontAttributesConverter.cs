using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that returns Bold font attributes when value is true, otherwise Normal
    /// </summary>
    public class BoolToFontAttributesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return FontAttributes.Bold;
            }
            return FontAttributes.None;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is FontAttributes attributes && attributes.HasFlag(FontAttributes.Bold);
        }
    }
}
