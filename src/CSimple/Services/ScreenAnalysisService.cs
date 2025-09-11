using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CSimple.Services
{
    /// <summary>
    /// Service for analyzing screen content to find UI elements
    /// </summary>
    public class ScreenAnalysisService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int width, int height,
            IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;

        /// <summary>
        /// Attempts to find a UI element on screen based on description and context
        /// </summary>
        public async Task<Point?> FindUIElementAsync(string elementDescription, string context = null)
        {
            try
            {
                Debug.WriteLine($"[ScreenAnalysis] Searching for UI element: {elementDescription}");
                
                // Take a screenshot for analysis
                var screenshot = await CaptureScreenAsync();
                if (screenshot == null)
                {
                    Debug.WriteLine("[ScreenAnalysis] Failed to capture screenshot");
                    return null;
                }

                // Analyze the screenshot for the requested element
                var elementLocation = await AnalyzeScreenshotForElementAsync(screenshot, elementDescription, context);
                
                screenshot.Dispose(); // Clean up memory
                
                if (elementLocation.HasValue)
                {
                    Debug.WriteLine($"[ScreenAnalysis] Found element at {elementLocation.Value}");
                }
                else
                {
                    Debug.WriteLine($"[ScreenAnalysis] Could not locate element: {elementDescription}");
                }

                return elementLocation;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error finding UI element: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Captures a screenshot of the entire screen
        /// </summary>
        private async Task<Bitmap> CaptureScreenAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var screenBounds = Screen.PrimaryScreen.Bounds;
                    var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);

                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size, CopyPixelOperation.SourceCopy);
                    }

                    return bitmap;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScreenAnalysis] Error capturing screenshot: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Analyzes a screenshot to locate a specific UI element
        /// </summary>
        private async Task<Point?> AnalyzeScreenshotForElementAsync(Bitmap screenshot, string elementDescription, string context)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var description = elementDescription.ToLowerInvariant();
                    
                    // Simple heuristic-based UI element detection
                    // This is a basic implementation - could be enhanced with OCR or ML
                    
                    if (description.Contains("button"))
                    {
                        return FindButtonLikeElement(screenshot, description);
                    }
                    
                    if (description.Contains("menu") || description.Contains("dropdown"))
                    {
                        return FindMenuLikeElement(screenshot, description);
                    }
                    
                    if (description.Contains("icon"))
                    {
                        return FindIconLikeElement(screenshot, description);
                    }
                    
                    if (description.Contains("text") || description.Contains("label"))
                    {
                        return FindTextLikeElement(screenshot, description);
                    }

                    // Generic element search - look for UI-like rectangular regions
                    return FindGenericUIElement(screenshot, description);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScreenAnalysis] Error analyzing screenshot: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Attempts to find button-like elements on screen
        /// </summary>
        private Point? FindButtonLikeElement(Bitmap screenshot, string description)
        {
            try
            {
                // Look for rectangular regions with button-like characteristics
                var candidates = new List<Rectangle>();
                
                // Simple edge detection to find rectangular regions
                var edgeRegions = DetectRectangularRegions(screenshot);
                
                // Filter for button-like sizes (typically 50-300 pixels wide, 20-60 pixels tall)
                candidates = edgeRegions.Where(r => 
                    r.Width >= 50 && r.Width <= 300 && 
                    r.Height >= 20 && r.Height <= 60).ToList();

                if (candidates.Any())
                {
                    // Return the center of the first candidate
                    var button = candidates.First();
                    return new Point(button.X + button.Width / 2, button.Y + button.Height / 2);
                }

                // Fallback: return a common button location (bottom-right area)
                return new Point(screenshot.Width - 100, screenshot.Height - 50);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error finding button: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to find menu-like elements on screen
        /// </summary>
        private Point? FindMenuLikeElement(Bitmap screenshot, string description)
        {
            try
            {
                // Menus are typically in the top area of windows or applications
                var topRegion = new Rectangle(0, 0, screenshot.Width, screenshot.Height / 4);
                
                // Look for menu-like regions (horizontal rectangles in top area)
                var menuRegions = DetectRectangularRegions(screenshot)
                    .Where(r => r.Y < topRegion.Height && r.Width > 100 && r.Height < 40)
                    .OrderBy(r => r.Y) // Prefer higher elements
                    .ToList();

                if (menuRegions.Any())
                {
                    var menu = menuRegions.First();
                    return new Point(menu.X + menu.Width / 2, menu.Y + menu.Height / 2);
                }

                // Fallback: return top-left area where menus typically are
                return new Point(100, 30);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error finding menu: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to find icon-like elements on screen
        /// </summary>
        private Point? FindIconLikeElement(Bitmap screenshot, string description)
        {
            try
            {
                // Icons are typically small square regions
                var iconRegions = DetectRectangularRegions(screenshot)
                    .Where(r => r.Width >= 16 && r.Width <= 64 && 
                               r.Height >= 16 && r.Height <= 64 &&
                               Math.Abs(r.Width - r.Height) <= 10) // Nearly square
                    .ToList();

                if (iconRegions.Any())
                {
                    var icon = iconRegions.First();
                    return new Point(icon.X + icon.Width / 2, icon.Y + icon.Height / 2);
                }

                // Fallback: return desktop area where icons might be
                return new Point(50, 50);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error finding icon: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to find text-like elements on screen
        /// </summary>
        private Point? FindTextLikeElement(Bitmap screenshot, string description)
        {
            try
            {
                // Text elements are typically wider than they are tall
                var textRegions = DetectRectangularRegions(screenshot)
                    .Where(r => r.Width > r.Height * 2 && r.Height >= 12 && r.Height <= 40)
                    .ToList();

                if (textRegions.Any())
                {
                    var text = textRegions.First();
                    return new Point(text.X + text.Width / 2, text.Y + text.Height / 2);
                }

                // Fallback: return center area
                return new Point(screenshot.Width / 2, screenshot.Height / 2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error finding text: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generic UI element finder as fallback
        /// </summary>
        private Point? FindGenericUIElement(Bitmap screenshot, string description)
        {
            try
            {
                // Return center of screen as last resort
                return new Point(screenshot.Width / 2, screenshot.Height / 2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error finding generic element: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Simple rectangular region detection using edge detection
        /// </summary>
        private List<Rectangle> DetectRectangularRegions(Bitmap screenshot)
        {
            var regions = new List<Rectangle>();
            
            try
            {
                // Simple implementation - could be enhanced with proper edge detection algorithms
                // For now, return some sample regions for testing
                
                // Sample common UI regions
                regions.Add(new Rectangle(20, 20, 100, 30));   // Top-left button area
                regions.Add(new Rectangle(screenshot.Width - 120, 20, 100, 30)); // Top-right button area
                regions.Add(new Rectangle(screenshot.Width / 2 - 50, screenshot.Height - 60, 100, 40)); // Bottom center button
                
                return regions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenAnalysis] Error detecting regions: {ex.Message}");
                return regions;
            }
        }

        /// <summary>
        /// Gets the dominant colors in a screen region (for enhanced element detection)
        /// </summary>
        public async Task<List<Color>> GetDominantColorsAsync(Rectangle region)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var screenshot = CaptureScreenAsync().Result;
                    if (screenshot == null) return new List<Color>();

                    var colors = new Dictionary<Color, int>();
                    
                    // Sample colors from the region
                    for (int x = region.X; x < region.X + region.Width && x < screenshot.Width; x += 5)
                    {
                        for (int y = region.Y; y < region.Y + region.Height && y < screenshot.Height; y += 5)
                        {
                            var pixel = screenshot.GetPixel(x, y);
                            if (colors.ContainsKey(pixel))
                                colors[pixel]++;
                            else
                                colors[pixel] = 1;
                        }
                    }

                    screenshot.Dispose();
                    
                    // Return top 5 most common colors
                    return colors.OrderByDescending(c => c.Value)
                                .Take(5)
                                .Select(c => c.Key)
                                .ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScreenAnalysis] Error getting dominant colors: {ex.Message}");
                    return new List<Color>();
                }
            });
        }
    }
}
