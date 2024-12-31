using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.IO;

namespace CSimple.Converters
{
    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string base64 && !string.IsNullOrWhiteSpace(base64))
            {
                if (string.IsNullOrWhiteSpace(base64))
                {
                    return null;
                }
                try
                {
                    var bytes = System.Convert.FromBase64String(base64);
                    return ImageSource.FromStream(() => new MemoryStream(bytes));
                }
                catch
                {
                    // Return null or a placeholder if not valid base64
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
