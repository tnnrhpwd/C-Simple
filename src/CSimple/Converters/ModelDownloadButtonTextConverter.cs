using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using CSimple.ViewModels;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that returns "Download to Device" or "Remove from Device" based on whether a model is downloaded.
    /// Expects the ConverterParameter to be the NetPageViewModel and the value to be the HuggingFace model ID.
    /// </summary>
    public class ModelDownloadButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value should be the HuggingFace model ID
            // parameter should be the NetPageViewModel (passed from XAML)

            if (value is string modelId && !string.IsNullOrEmpty(modelId) && parameter is NetPageViewModel viewModel)
            {
                return viewModel.IsModelDownloaded(modelId) ? "Remove from Device" : "Download to Device";
            }

            // Default fallback
            return "Download to Device";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not implemented for ModelDownloadButtonTextConverter");
        }
    }
}
