using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    public class FloatToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float floatValue && parameter is string stringParameter)
            {
                if (float.TryParse(stringParameter, out float maxWidth))
                {
                    // Ensure the value is between 0 and 1
                    floatValue = Math.Clamp(floatValue, 0f, 1f);

                    // Convert to width based on maxWidth
                    return floatValue * maxWidth;
                }
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
