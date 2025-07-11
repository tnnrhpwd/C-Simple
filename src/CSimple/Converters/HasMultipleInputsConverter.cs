using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    public class HasMultipleInputsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int inputCount)
            {
                return inputCount > 1;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
