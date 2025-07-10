using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using CSimple.ViewModels;

namespace CSimple.Converters
{
    public class StepContentTypeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2 || parameter == null)
                return false;

            // First value: StepContentType (string)
            var stepContentType = values[0] as string;

            // Second value: SelectedNode (NodeViewModel)
            var selectedNode = values[1] as NodeViewModel;

            // Use StepContentType if available, otherwise fall back to node's DataType
            var effectiveContentType = !string.IsNullOrEmpty(stepContentType) ? stepContentType : selectedNode?.DataType;

            // Check if the effective content type matches the parameter
            return string.Equals(effectiveContentType, parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
