using System.Globalization;
using Microsoft.Maui.Graphics;

namespace CSimple.Converters
{
    public class IntToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                if (parameter is string paramString)
                {
                    // Parameter format: "threshold|zeroColor|nonZeroColor"
                    var parts = paramString.Split('|');
                    if (parts.Length == 3 && int.TryParse(parts[0], out int threshold))
                    {
                        var colorString = intValue <= threshold ? parts[1] : parts[2];
                        if (Color.TryParse(colorString, out var color))
                        {
                            return color;
                        }
                    }
                }

                // Default colors if no parameter or invalid parameter
                return intValue > 0 ? Colors.Green : Colors.Red;
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
