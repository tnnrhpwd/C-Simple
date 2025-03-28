using System.Globalization;
using Microsoft.Maui.Graphics;

namespace CSimple.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string paramString)
                {
                    // Parameter format: "TrueColor|FalseColor"
                    var colors = paramString.Split('|');
                    if (colors.Length == 2)
                    {
                        var colorString = boolValue ? colors[0] : colors[1];
                        if (Color.TryParse(colorString, out var color))
                        {
                            return color;
                        }
                    }
                }

                // Default colors if no parameter or invalid parameter
                return boolValue ? Colors.Green : Colors.Gray;
            }

            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support two-way binding
            throw new NotImplementedException();
        }
    }
}
