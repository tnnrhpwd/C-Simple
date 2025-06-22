using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using CSimple.Models;
using CSimple.ViewModels;
using System.Linq;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that maps ModelInputType enum to ModelInputTypeDisplayItem and vice versa
    /// </summary>
    public class EnumToDisplayItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"EnumToDisplayItemConverter.Convert: value={value}, type={value?.GetType()?.Name}");

            if (value is ModelInputType inputType && parameter is NetPageViewModel viewModel)
            {
                var item = viewModel.ModelInputTypeDisplayItems?.FirstOrDefault(x => x.Value == inputType);
                System.Diagnostics.Debug.WriteLine($"EnumToDisplayItemConverter.Convert: {inputType} -> {item?.DisplayName ?? "null"}");
                return item;
            }

            System.Diagnostics.Debug.WriteLine($"EnumToDisplayItemConverter.Convert: Returning null for value {value}");
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"EnumToDisplayItemConverter.ConvertBack: value={value}, type={value?.GetType()?.Name}");

            if (value is NetPageViewModel.ModelInputTypeDisplayItem item)
            {
                System.Diagnostics.Debug.WriteLine($"EnumToDisplayItemConverter.ConvertBack: {item.DisplayName} -> {item.Value}");
                return item.Value;
            }

            System.Diagnostics.Debug.WriteLine($"EnumToDisplayItemConverter.ConvertBack: Returning Unknown for value {value}");
            return ModelInputType.Unknown;
        }
    }
}
