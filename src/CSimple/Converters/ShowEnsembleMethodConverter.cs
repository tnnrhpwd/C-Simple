using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using CSimple.ViewModels;

namespace CSimple.Converters
{
    public class ShowEnsembleMethodConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NodeViewModel node)
            {
                // Show ensemble method selector if:
                // 1. Node is a model AND
                // 2. Node has multiple inputs (EnsembleInputCount > 1)
                return node.IsModel && node.EnsembleInputCount > 1;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
