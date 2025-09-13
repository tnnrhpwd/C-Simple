using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using CSimple.Models;
using CSimple.ViewModels;
using static CSimple.ViewModels.NetPageViewModel;

namespace CSimple.Converters
{
    /// <summary>
    /// Converter that maps between ModelInputTypeDisplayItem and ModelInputType enum
    /// </summary>
    public class ModelInputTypeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ModelInputType inputType)
            {
                // Find the matching display item
                return inputType switch
                {
                    ModelInputType.Text => new ModelInputTypeDisplayItem { Value = ModelInputType.Text, DisplayName = "Text" },
                    ModelInputType.Image => new ModelInputTypeDisplayItem { Value = ModelInputType.Image, DisplayName = "Image" },
                    ModelInputType.Audio => new ModelInputTypeDisplayItem { Value = ModelInputType.Audio, DisplayName = "Audio" },
                    ModelInputType.Multimodal => new ModelInputTypeDisplayItem { Value = ModelInputType.Multimodal, DisplayName = "Multimodal (Vision + Text)" },
                    ModelInputType.Unknown => new ModelInputTypeDisplayItem { Value = ModelInputType.Unknown, DisplayName = "Unknown" },
                    _ => new ModelInputTypeDisplayItem { Value = ModelInputType.Unknown, DisplayName = "Unknown" }
                };
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ModelInputTypeDisplayItem displayItem)
            {
                return displayItem.Value;
            }

            return ModelInputType.Unknown;
        }
    }
}
