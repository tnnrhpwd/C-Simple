using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using System.Diagnostics; // Added for Debug.WriteLine

namespace CSimple.Converters
{
    public class EqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Debug.WriteLine($"EqualsConverter: Input value = '{value}', Parameter = '{parameter}'");

            if (value == null || parameter == null)
            {
                // Debug.WriteLine("EqualsConverter: Value or Parameter is null, returning false.");
                return false;
            }

            string valueString;
            if (value is Enum)
            {
                valueString = Enum.GetName(value.GetType(), value);
                // Debug.WriteLine($"EqualsConverter: Value is Enum, converted to string '{valueString}'");
            }
            else
            {
                valueString = value.ToString();
                // Debug.WriteLine($"EqualsConverter: Value is not Enum, converted to string '{valueString}'");
            }

            string parameterString = parameter.ToString();
            // Debug.WriteLine($"EqualsConverter: Parameter converted to string '{parameterString}'");

            bool result = string.Equals(valueString, parameterString, StringComparison.OrdinalIgnoreCase);
            Debug.WriteLine($"EqualsConverter: Value '{valueString}' == Parameter '{parameterString}' (IgnoreCase)? {result}");

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotImplementedException();
        }
    }
}
