using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using CSimple.Models;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that transforms enum values into user-friendly display strings
    /// </summary>
    public class EnumToDisplayStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            if (value is ModelInputType inputType)
            {
                return inputType switch
                {
                    ModelInputType.Text => "Text",
                    ModelInputType.Image => "Image",
                    ModelInputType.Audio => "Audio",
                    ModelInputType.Unknown => "Unknown",
                    _ => value.ToString()
                };
            }

            // For other enums, just return the name
            if (value is Enum)
            {
                return Enum.GetName(value.GetType(), value) ?? value.ToString();
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var stringValue = value.ToString();

            if (targetType == typeof(ModelInputType) || targetType == typeof(ModelInputType?))
            {
                return stringValue switch
                {
                    "Text" => ModelInputType.Text,
                    "Image" => ModelInputType.Image,
                    "Audio" => ModelInputType.Audio,
                    "Unknown" => ModelInputType.Unknown,
                    _ => ModelInputType.Unknown
                };
            }

            // For other enum types, try to parse
            if (targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, stringValue);
                }
                catch
                {
                    // Return default value if parsing fails
                    return Activator.CreateInstance(targetType);
                }
            }

            return value;
        }
    }
}
