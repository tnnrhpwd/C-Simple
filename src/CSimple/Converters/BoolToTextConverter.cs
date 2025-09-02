using System.Diagnostics;
using System.Globalization;

namespace CSimple.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public string TrueText { get; set; }
        public string FalseText { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                Debug.WriteLine($"BoolToTextConverter Value is a boolean: {boolValue}");

                // If parameter is provided in format "TrueText|FalseText", parse it
                if (parameter is string paramString && paramString.Contains("|"))
                {
                    var parts = paramString.Split('|');
                    if (parts.Length == 2)
                    {
                        return boolValue ? parts[0] : parts[1];
                    }
                }

                // Fallback to properties
                return boolValue ? TrueText : FalseText;
            }
            Debug.WriteLine("BoolToTextConverter Value is not a boolean");

            // Return FalseText or second part of parameter as fallback
            if (parameter is string paramString2 && paramString2.Contains("|"))
            {
                var parts = paramString2.Split('|');
                if (parts.Length == 2)
                {
                    return parts[1]; // Return false text
                }
            }

            return FalseText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}