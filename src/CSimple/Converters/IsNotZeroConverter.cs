using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    public class IsNotZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int intValue && intValue > 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
