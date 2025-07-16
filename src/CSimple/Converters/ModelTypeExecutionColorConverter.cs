using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter to change text color when a specific model type is executing
    /// </summary>
    public class ModelTypeExecutionColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values?.Length >= 3 &&
                values[0] is string labelType &&
                values[1] is string currentExecutingModelType &&
                values[2] is bool isExecuting)
            {
                // Check if the model type matches and execution is active
                bool isCurrentTypeExecuting = isExecuting &&
                    !string.IsNullOrEmpty(currentExecutingModelType) &&
                    string.Equals(labelType, currentExecutingModelType, StringComparison.OrdinalIgnoreCase);

                if (isCurrentTypeExecuting)
                {
                    // Return highlighting color when this type is executing
                    return Colors.Orange;
                }
            }

            // Return default theme-aware color
            return Application.Current.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#E0E0E0") // Light gray for dark theme
                : Color.FromArgb("#404040"); // Dark gray for light theme
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
