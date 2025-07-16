using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that returns Orange color when executing (true), otherwise returns the default text color
    /// </summary>
    public class BoolToExecutingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return Colors.Orange; // Currently executing
            }

            // Return appropriate text color based on theme
            bool isDarkTheme = Application.Current.RequestedTheme == AppTheme.Dark;
            if (Application.Current.Resources.TryGetValue(isDarkTheme ? "TextSecondaryDark" : "TextSecondaryLight", out object textColorObj))
            {
                return (Color)textColorObj;
            }

            // Fallback colors
            return isDarkTheme ? Colors.LightGray : Colors.DarkGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
