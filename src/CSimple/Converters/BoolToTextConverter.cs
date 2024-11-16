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
            if (value is bool boolean)
            {
                string result = boolean ? "Stop" : "Simulate";
                Debug.WriteLine($"Conversion result: {result}: Convert called with value: {value}, targetType: {targetType}, parameter: {parameter}, culture: {culture}");
                return result;
            }
            Debug.WriteLine("Conversion failed: value is not a boolean");
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"ConvertBack called with value: {value}, targetType: {targetType}, parameter: {parameter}, culture: {culture}");
            throw new NotImplementedException();
        }
    }
}