using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that transforms a boolean value to appropriate button text for system monitoring
    /// </summary>
    public class BoolToMonitoringButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? "Stop" : "Start";
            }
            return "Start";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
