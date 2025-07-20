using System.Globalization;
using System.IO;

namespace CSimple.Converters
{
    /// <summary>
    /// Safely converts a file path to an ImageSource, returning null if the file doesn't exist
    /// This prevents crashes when image files are missing or inaccessible
    /// </summary>
    public class SafeImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string filePath && !string.IsNullOrEmpty(filePath))
            {
                try
                {
                    // Check if file exists before creating ImageSource
                    if (File.Exists(filePath))
                    {
                        return ImageSource.FromFile(filePath);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[SafeImageSourceConverter] Image file not found: {filePath}");

                        // Return a placeholder or null instead of crashing
                        if (parameter is string placeholderPath && !string.IsNullOrEmpty(placeholderPath))
                        {
                            return ImageSource.FromFile(placeholderPath);
                        }

                        return null; // Let the Image control handle the null gracefully
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SafeImageSourceConverter] Error loading image {filePath}: {ex.Message}");

                    // Return placeholder or null on any error
                    if (parameter is string placeholderPath && !string.IsNullOrEmpty(placeholderPath))
                    {
                        try
                        {
                            return ImageSource.FromFile(placeholderPath);
                        }
                        catch
                        {
                            return null;
                        }
                    }

                    return null;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
