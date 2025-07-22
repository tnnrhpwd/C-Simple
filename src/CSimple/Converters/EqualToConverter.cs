using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter to check if a value equals a parameter
    /// </summary>
    public class EqualToConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null && parameter == null)
                return true;
            
            if (value == null || parameter == null)
                return false;

            return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
