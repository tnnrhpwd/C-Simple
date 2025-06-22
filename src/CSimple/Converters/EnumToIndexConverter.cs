using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using CSimple.Models;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that maps ModelInputType enum to picker index and vice versa
    /// </summary>
    public class EnumToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"EnumToIndexConverter.Convert: value={value}, type={value?.GetType()?.Name}");

            if (value is ModelInputType inputType)
            {
                // Map enum values to their corresponding indices
                var result = inputType switch
                {
                    ModelInputType.Text => 0,
                    ModelInputType.Image => 1,
                    ModelInputType.Audio => 2,
                    ModelInputType.Unknown => 3,
                    _ => 3 // Default to Unknown
                };

                System.Diagnostics.Debug.WriteLine($"EnumToIndexConverter.Convert: {inputType} -> {result}");
                return result;
            }

            System.Diagnostics.Debug.WriteLine($"EnumToIndexConverter.Convert: Returning default 3 for value {value}");
            return 3; // Default to Unknown index
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"EnumToIndexConverter.ConvertBack: value={value}, type={value?.GetType()?.Name}");

            if (value is int index)
            {
                // Map indices back to enum values
                var result = index switch
                {
                    0 => ModelInputType.Text,
                    1 => ModelInputType.Image,
                    2 => ModelInputType.Audio,
                    3 => ModelInputType.Unknown,
                    _ => ModelInputType.Unknown
                };

                System.Diagnostics.Debug.WriteLine($"EnumToIndexConverter.ConvertBack: {index} -> {result}");
                return result;
            }

            System.Diagnostics.Debug.WriteLine($"EnumToIndexConverter.ConvertBack: Returning default Unknown for value {value}");
            return ModelInputType.Unknown;
        }
    }
}
