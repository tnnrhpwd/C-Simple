using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using CSimple.ViewModels;
using CSimple.Models;

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

            // For model nodes, ONLY use the explicit StepContentType, never fall back to DataType
            // because model nodes can process one type of input (e.g., images) and output another type (e.g., text)
            if (selectedNode?.Type == NodeType.Model)
            {
                // Only use stepContentType for model nodes - don't fall back to node's DataType
                // If stepContentType is null/empty, this means no output has been generated yet
                if (string.IsNullOrEmpty(stepContentType))
                {
                    // No output generated yet - don't show any content type-specific UI
                    return false;
                }

                return string.Equals(stepContentType, parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // For input/other nodes, use StepContentType if available, otherwise fall back to node's DataType
                var effectiveContentType = !string.IsNullOrEmpty(stepContentType) ? stepContentType : selectedNode?.DataType;
                return string.Equals(effectiveContentType, parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
