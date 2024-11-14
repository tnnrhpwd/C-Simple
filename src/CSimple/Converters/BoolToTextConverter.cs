using System;
using System.Globalization;
using System.Diagnostics;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine("message");
            if (value is bool boolean)
            {
                return boolean ? "Simulate" : "Stop";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}