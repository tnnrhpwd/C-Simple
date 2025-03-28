using System.Globalization;

namespace CSimple.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                if (parameter is string paramString && int.TryParse(paramString, out int threshold))
                {
                    return intValue == threshold;
                }
                
                // Default behavior: non-zero is true
                return intValue != 0;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support two-way binding
            throw new NotImplementedException();
        }
    }
}
