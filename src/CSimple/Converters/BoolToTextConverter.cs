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
                return boolValue ? TrueText : FalseText;
            }
            Debug.WriteLine("BoolToTextConverter Value is not a boolean");
            return FalseText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}