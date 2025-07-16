using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter to determine visibility of execution status panel
    /// Shows when models are executing or when there are recent execution results
    /// </summary>
    public class ExecutionStatusVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExecuting)
            {
                return isExecuting;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
