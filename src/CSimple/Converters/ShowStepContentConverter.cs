using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using CSimple.ViewModels;

namespace CSimple.Converters
{
    public class ShowStepContentConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return false;

            // First value: CurrentActionStep (int)
            var currentActionStep = values[0] is int step ? step : 0;

            // Second value: SelectedNode (NodeViewModel)
            var selectedNode = values[1] as NodeViewModel;

            // Show step content if:
            // 1. There's an active action review step (CurrentActionStep > 0), OR
            // 2. The selected node is a model node (to show generated output)
            return currentActionStep > 0 || (selectedNode?.IsModel == true);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
