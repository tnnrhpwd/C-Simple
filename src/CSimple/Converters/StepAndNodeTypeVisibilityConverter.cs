using System;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;

namespace CSimple.Converters
{
    public class StepAndNodeTypeVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 || parameter == null)
                return false;

            // values[0] is expected to be StepContentType (string)
            // values[1] is expected to be SelectedNode.DataType (string)
            string stepContentType = values[0] as string;
            string selectedNodeDataType = values[1] as string;
            string targetDataType = parameter as string;

            if (string.IsNullOrEmpty(stepContentType) ||
                string.IsNullOrEmpty(selectedNodeDataType) ||
                string.IsNullOrEmpty(targetDataType))
            {
                return false;
            }

            bool stepContentTypeMatches = string.Equals(stepContentType, targetDataType, StringComparison.OrdinalIgnoreCase);
            bool nodeDataTypeMatches = string.Equals(selectedNodeDataType, targetDataType, StringComparison.OrdinalIgnoreCase);

            return stepContentTypeMatches && nodeDataTypeMatches;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
