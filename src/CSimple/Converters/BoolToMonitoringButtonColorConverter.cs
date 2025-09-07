using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that transforms a boolean value to appropriate button color for system monitoring
    /// </summary>
    public class BoolToMonitoringButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? Colors.Red : Colors.Green;
            }
            return Colors.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
