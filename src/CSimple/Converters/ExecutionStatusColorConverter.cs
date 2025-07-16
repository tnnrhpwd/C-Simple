using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter to determine color based on execution status
    /// </summary>
    public class ExecutionStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                var statusLower = status.ToLowerInvariant();

                if (statusLower.Contains("error") || statusLower.Contains("failed"))
                {
                    return Colors.Red;
                }
                else if (statusLower.Contains("completed") || statusLower.Contains("successful"))
                {
                    return Colors.Green;
                }
                else if (statusLower.Contains("executing") || statusLower.Contains("processing") || statusLower.Contains("preparing"))
                {
                    return Colors.Orange;
                }
                else if (statusLower.Contains("ready"))
                {
                    return Colors.Blue;
                }
            }

            // Default color for unknown status
            return Application.Current.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.DarkGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
