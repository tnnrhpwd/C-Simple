using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using System.Diagnostics;

#if WINDOWS
using System.Windows.Forms;
using System.Runtime.InteropServices;
#endif
using Microsoft.Maui.Controls;

namespace CSimple.Services
{
    public class ScreenCaptureService
    {
        #region Events
        public event Action<string> FileCaptured;
        public event Action<ImageSource> ScreenPreviewFrameReady;
        public event Action<ImageSource> WebcamPreviewFrameReady;
        #endregion

        #region Properties
        private string _screenshotsDirectory;
        private string _webcamImagesDirectory;
        private bool _previewModeActive = false;
        private CancellationTokenSource _previewCts;

        // Reduced capture interval to reduce overhead
        private const int CaptureIntervalMs = 250; // Capture every 250ms (4 times per second)
        #endregion

        #region Windows API
#if WINDOWS
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int xDest, int yDest, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool RegisterTouchWindow(IntPtr hwnd, uint ulFlags);

        [DllImport("user32.dll")]
        private static extern bool IsTouchWindow(IntPtr hwnd, out uint pulFlags);

        [DllImport("user32.dll")]
        private static extern bool EnableMouseInPointer(bool enable);

        [DllImport("user32.dll")]
        private static extern bool SetProcessPointerDevices(bool fProcessInputDevices);

        [DllImport("user32.dll")]
        private static extern bool EnableProcessMouseWheelFiltering(bool enable);

        private const int HORZRES = 8;
        private const int VERTRES = 10;
        private const int SRCCOPY = 0x00CC0020;

        private const uint TWF_WANTPALM = 0x00000002;
        private const uint TWF_FINETOUCH = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
#endif
        #endregion

        public ScreenCaptureService()
        {
            InitDirectories();
        }

        private void InitDirectories()
        {
            string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            _screenshotsDirectory = Path.Combine(baseDirectory, "Screenshots");
            _webcamImagesDirectory = Path.Combine(baseDirectory, "WebcamImages");

            Directory.CreateDirectory(_screenshotsDirectory);
            Directory.CreateDirectory(_webcamImagesDirectory);

            Debug.Print($"Initialized image directories initialized: {_screenshotsDirectory}, {_webcamImagesDirectory}");
        }

        public void CaptureScreens(string actionName, string userTouchInputText)
        {
#if WINDOWS
            try
            {
                if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(userTouchInputText))
                    return;

                DateTime captureTime = DateTime.Now;

                foreach (var screen in Screen.AllScreens)
                {
                    var bounds = screen.Bounds;
                    // Use a single bitmap and graphics object to reduce allocations
                    using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(bitmap))
                        {
                            // Optimize: Use CopyFromScreen with handle
                            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                        }

                        string filePath = Path.Combine(_screenshotsDirectory, $"ScreenCapture_{captureTime:yyyyMMdd_HHmmss_fff}_{screen.DeviceName.Replace("\\", "").Replace(":", "")}.png");
                        bitmap.Save(filePath, ImageFormat.Png);

                        Debug.Print($"Screenshot saved to: {filePath}");
                        FileCaptured?.Invoke(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error capturing screen: {ex.Message}");
            }
#endif
        }

        public Task StartScreenCapture(CancellationToken cancellationToken, string actionName, string userTouchInputText)
        {
            return Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    CaptureScreens(actionName, userTouchInputText);
                    Thread.Sleep(CaptureIntervalMs); // Capture at the reduced interval
                }
            }, cancellationToken);
        }

        public Task StartWebcamCapture(CancellationToken cancellationToken, string actionName, string userTouchInputText)
        {
            Debug.WriteLine("[ScreenCaptureService] StartWebcamCapture called");
            if (!string.IsNullOrEmpty(actionName) || !string.IsNullOrEmpty(userTouchInputText))
            {
                Debug.Print("[ScreenCaptureService] Starting webcam capture with action or user input");
            }
            else
            {
                Debug.Print("Webcam capture started without action or user input");
            }
            return Task.Run(() =>
            {
                Debug.WriteLine("[ScreenCaptureService] Webcam capture task started");
                using var capture = new VideoCapture(0);
                using var frame = new Mat();

                if (!capture.IsOpened())
                {
                    Debug.Print("Failed to open webcam.");
                    return;
                }
                else
                {
                    Debug.Print("Webcam opened successfully.");
                }
                // Ensure the webcam is ready
                if (!capture.Read(frame) || frame.Empty())
                {
                    Debug.Print("Failed to read initial frame from webcam.");
                    return;
                }
                else
                {
                    Debug.Print("Initial webcam frame read successfully.");
                }

                Debug.WriteLine($"[ScreenCaptureService] Webcam capture loop - CancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!capture.Read(frame))
                        {
                            Debug.Print("Failed to read frame from webcam.");
                            continue;
                        }
                        else if (frame.Width == 0 || frame.Height == 0)
                        {
                            Debug.Print("Webcam frame has zero dimensions.");
                            continue;
                        }
                        else if (frame.Width < 100 || frame.Height < 100)
                        {
                            Debug.Print("Webcam frame is too small.");
                            continue;
                        }
                        else if (frame.Width > 1920 || frame.Height > 1080)
                        {
                            Debug.Print("Webcam frame is too large.");
                            continue;
                        }
                        else if (frame.Channels() != 3 && frame.Channels() != 4)
                        {
                            Debug.Print("Webcam frame has unexpected number of channels.");
                            continue;
                        }
                        else if (frame.Depth() != MatType.CV_8U)
                        {
                            Debug.Print("Webcam frame has unexpected depth.");
                            continue;
                        }
                        else if (frame.Type() != MatType.CV_8UC3 && frame.Type() != MatType.CV_8UC4)
                        {
                            Debug.Print("Webcam frame has unexpected type.");
                            continue;
                        }
                        else if (frame.Rows < 10 || frame.Cols < 10)
                        {
                            Debug.Print("Webcam frame is too small.");
                            continue;
                        }
                        else
                        {
                            Debug.Print("Webcam frame captured successfully.");
                        }

                        if (frame.Empty())
                        {
                            Debug.Print("Webcam frame is empty.");
                            continue;
                        }

                        if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(userTouchInputText))
                        {
                            string filePath = Path.Combine(_webcamImagesDirectory, $"WebcamImage_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                            try
                            {
                                Debug.WriteLine($"[ScreenCaptureService] Attempting to save webcam image to: {filePath}, _previewModeActive: {_previewModeActive}");
                                Cv2.ImWrite(filePath, frame);
                                Debug.Print($"Webcam image saved to: {filePath}");
                                FileCaptured?.Invoke(filePath);
                            }
                            catch (Exception ex)
                            {
                                Debug.Print($"Error saving webcam image: {ex.Message}");
                            }
                        }
                        Thread.Sleep(CaptureIntervalMs);
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"Exception in webcam capture loop: {ex.Message}");
                    }
                }
                Debug.Print("Webcam capture stopped by cancellation request.");
            }, cancellationToken);
        }

        public void StartPreviewMode()
        {
            _previewModeActive = true;
            _previewCts = new CancellationTokenSource();

            // Start sending preview frames
            Task.Run(() => GeneratePreviewFrames(_previewCts.Token));

            Debug.Print("Screen capture preview mode started");
        }

        public void StopPreviewMode()
        {
            _previewModeActive = false;
            Debug.WriteLine("[ScreenCaptureService] StopPreviewMode - Calling _previewCts?.Cancel()");
            _previewCts?.Cancel();
            _previewCts = null;

            Debug.Print("Screen capture preview mode stopped");
        }

        private async Task GeneratePreviewFrames(CancellationToken token)
        {
            try
            {
                // Set up webcam for preview
                using var webcamCapture = new VideoCapture(0);
                using var webcamFrame = new Mat();

                if (!webcamCapture.IsOpened())
                {
                    Debug.Print("Failed to open webcam for preview.");
                }
                else
                {
                    Debug.Print("Webcam opened successfully for preview.");
                }

                while (!token.IsCancellationRequested && _previewModeActive)
                {
                    try
                    {
                        // 1. Get the screen preview
                        var screenImage = CaptureScreenForPreview();
                        if (screenImage != null)
                        {
                            // Make sure we're on a background thread when invoking the event
                            ScreenPreviewFrameReady?.Invoke(screenImage);
                        }

                        // 2. Get the webcam preview
                        if (webcamCapture.IsOpened())
                        {
                            if (webcamCapture.Read(webcamFrame) && !webcamFrame.Empty())
                            {
                                Debug.WriteLine("[ScreenCaptureService] GeneratePreviewFrames - Webcam frame read successfully");
                                var webcamImage = ConvertMatToImageSource(webcamFrame);
                                if (webcamImage != null)
                                {
                                    WebcamPreviewFrameReady?.Invoke(webcamImage);
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[ScreenCaptureService] GeneratePreviewFrames - Failed to read webcam frame");
                            }
                        }

                        // Short delay for next frame
                        await Task.Delay(200, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // Rethrow to exit the loop
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"Frame capture error: {ex.Message}");
                        await Task.Delay(1000, token); // Longer delay on error
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Debug.Print("Preview mode cancelled");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error in preview generation: {ex.Message}");
            }
        }

        private ImageSource CaptureScreenForPreview()
        {
#if WINDOWS
            try
            {
                // Get the primary screen
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        // Optimize: Use CopyFromScreen with handle
                        g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);

                        // Calculate aspect ratio and determine target size
                        double aspectRatio = (double)bounds.Width / bounds.Height;
                        int targetHeight = 360;
                        int targetWidth = (int)(targetHeight * aspectRatio);

                        // Resize for preview while maintaining aspect ratio
                        var resizedBitmap = new Bitmap(bitmap, new System.Drawing.Size(
                            targetWidth, targetHeight));

                        // Convert to a format that MAUI can display
                        using (var memoryStream = new MemoryStream())
                        {
                            // Use JPEG for smaller file size
                            resizedBitmap.Save(memoryStream, ImageFormat.Jpeg);
                            memoryStream.Position = 0;

                            // Create a copy to avoid memory issues
                            var data = memoryStream.ToArray();

                            return ImageSource.FromStream(() => new MemoryStream(data));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Screen capture error: {ex.Message}");
            }
#endif
            return null;
        }

        private ImageSource ConvertMatToImageSource(Mat frame)
        {
            try
            {
                // Calculate aspect ratio to maintain proportions
                double aspectRatio = (double)frame.Width / frame.Height;
                int targetHeight = 240;
                int targetWidth = (int)(targetHeight * aspectRatio);

                // Resize the frame for better performance while maintaining aspect ratio
                using var resizedFrame = new Mat();
                Cv2.Resize(frame, resizedFrame, new OpenCvSharp.Size(targetWidth, targetHeight));

                // Create a temporary file with a unique name
                string tempFile = Path.Combine(Path.GetTempPath(), $"webcam_{Guid.NewGuid()}.jpg");

                // Save the frame to a file with higher quality
                // Use OpenCvSharp's correct parameter syntax
                var imgParams = new int[] { (int)ImwriteFlags.JpegQuality, 95 };
                Cv2.ImWrite(tempFile, resizedFrame, imgParams);

                // Load the file as an ImageSource
                var imageSource = ImageSource.FromFile(tempFile);

                // Schedule the file for deletion
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000);
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                    catch { /* Ignore cleanup errors */ }
                });

                return imageSource;
            }
            catch (Exception ex)
            {
                Debug.Print($"Error converting webcam frame: {ex.Message}");
                return null;
            }
        }

        // Add this new method to get a single screenshot
        public ImageSource GetSingleScreenshot()
        {
            return CaptureScreenForPreview();
        }

        public void Initialize(IntPtr windowHandle)
        {
#if WINDOWS
            // Register the window for touch input with both fine-touch and palm rejection options
            bool result = RegisterTouchWindow(windowHandle, TWF_FINETOUCH | TWF_WANTPALM);

            // Verify the window was registered for touch
            uint flags;
            bool isTouchRegistered = IsTouchWindow(windowHandle, out flags);

            Debug.Print($"Touch registration result: {result}, Is touch window: {isTouchRegistered}, Flags: {flags}");

            // Enable enhanced pointer support for Windows 8+ touch and pen support
            try
            {
                // Enable Windows Pointer messages instead of legacy mouse messages
                EnableMouseInPointer(true);

                // Ensure process handles multiple pointer devices
                SetProcessPointerDevices(true);

                // Fine-tune mouse wheel behavior
                EnableProcessMouseWheelFiltering(true);

                Debug.Print("Enhanced pointer input enabled");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error enabling enhanced pointer input: {ex.Message}");
            }
#else
            Debug.Print("Touch input registration not available on this platform");
#endif
        }

        public string GetMostRecentScreenshot(DateTime timestamp)
        {
            return GetMostRecentFile(_screenshotsDirectory, timestamp, "ScreenCapture");
        }

        public string GetMostRecentWebcamImage(DateTime timestamp)
        {
            return GetMostRecentFile(_webcamImagesDirectory, timestamp, "WebcamImage");
        }

        private string GetMostRecentFile(string directory, DateTime timestamp, string filePrefix)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Debug.Print($"Directory not found: {directory}");
                    return null;
                }

                var files = Directory.GetFiles(directory)
                    .Where(f => Path.GetFileName(f).StartsWith(filePrefix))
                    .Select(f => new
                    {
                        Path = f,
                        CreationTime = GetDateTimeFromFilename(f)
                    })
                    .Where(f => f.CreationTime <= timestamp)
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (files.Any())
                {
                    return files.First().Path;
                }
                else
                {
                    Debug.Print($"No files found in {directory} for timestamp {timestamp}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error getting most recent file: {ex.Message}");
                return null;
            }
        }

        private DateTime GetDateTimeFromFilename(string filename)
        {
            try
            {
                // Example filename: ScreenCapture_20250427_150419_512_.DISPLAY2.png
                // Example filename: WebcamImage_20250427_150419.jpg
                string name = Path.GetFileNameWithoutExtension(filename);
                string dateTimePart = name.Substring(name.IndexOf('_') + 1, 19); // Extract yyyyMMdd_HHmmss_fff or yyyyMMdd_HHmmss

                if (DateTime.TryParseExact(dateTimePart, "yyyyMMdd_HHmmss_fff", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDateTime))
                {
                    return parsedDateTime;
                }
                else if (DateTime.TryParseExact(dateTimePart, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out parsedDateTime))
                {
                    return parsedDateTime;
                }
                else
                {
                    Debug.Print($"Could not parse DateTime from filename: {filename}");
                    return DateTime.MinValue;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error parsing DateTime from filename: {ex.Message}");
                return DateTime.MinValue;
            }
        }
    }
}
