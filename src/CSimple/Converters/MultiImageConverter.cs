using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSimple.Converters
{
    /// <summary>
    /// Converts a semicolon-separated string of image paths to a list of ImageSource objects
    /// </summary>
    public class MultiImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string imagePathsString && !string.IsNullOrEmpty(imagePathsString))
            {
                // Split by semicolon to handle multiple images
                var imagePaths = imagePathsString.Split(';', StringSplitOptions.RemoveEmptyEntries);

                var imageSources = new List<ImageSource>();
                foreach (var path in imagePaths)
                {
                    try
                    {
                        imageSources.Add(ImageSource.FromFile(path.Trim()));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MultiImageConverter] Error loading image {path}: {ex.Message}");
                    }
                }

                return imageSources;
            }

            return new List<ImageSource>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Determines if the content contains multiple images (semicolon-separated paths)
    /// </summary>
    public class IsMultiImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string imagePathsString && !string.IsNullOrEmpty(imagePathsString))
            {
                return imagePathsString.Contains(';');
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
