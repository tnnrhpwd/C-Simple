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

        // CRITICAL FIX: Single shared webcam instance to prevent conflicts
        private static VideoCapture _sharedWebcamCapture = null;
        private static readonly object _webcamLock = new object();
        private static int _webcamUserCount = 0;
        private static bool _isWebcamInitialized = false;

        // Reduced capture interval to reduce overhead
        private const int CaptureIntervalMs = 250; // Capture every 250ms (4 times per second)
        #endregion

        // Add IsPreviewEnabled property
        public bool IsPreviewEnabled { get; set; }

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

        public void CaptureScreens(string actionName)
        {
#if WINDOWS
            try
            {
                // Debug.Print("[ScreenCaptureService] CaptureScreens called");
                if (string.IsNullOrEmpty(actionName))
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
                        // Debug.Print($"[ScreenCaptureService] CaptureScreens - About to save screenshot to: {filePath}"); // Added debug print
                        bitmap.Save(filePath, ImageFormat.Png);

                        // Debug.Print($"[ScreenCaptureService] CaptureScreens - Screenshot saved to: {filePath}");
                        FileCaptured?.Invoke(filePath);

                        // Convert the captured bitmap to ImageSource for preview
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            bitmap.Save(memoryStream, ImageFormat.Jpeg);
                            memoryStream.Position = 0;
                            byte[] data = memoryStream.ToArray();
                            ImageSource screenImage = ImageSource.FromStream(() => new MemoryStream(data));
                            ScreenPreviewFrameReady?.Invoke(screenImage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error capturing screen: {ex.Message}");
            }
#endif
        }

        // New method to capture screen for preview without saving
        private ImageSource CaptureScreenForPreview()
        {
#if WINDOWS
            try
            {
                // Just capture the primary screen for preview to avoid performance issues
                var screen = Screen.PrimaryScreen;
                var bounds = screen.Bounds;

                using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                    }

                    // Convert to ImageSource for preview - resize for better performance
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        // Resize the bitmap for preview (maintain aspect ratio)
                        double aspectRatio = (double)bitmap.Width / bitmap.Height;
                        int targetHeight = 240;
                        int targetWidth = (int)(targetHeight * aspectRatio);

                        using (var resizedBitmap = new Bitmap(targetWidth, targetHeight))
                        {
                            using (var resizeGraphics = Graphics.FromImage(resizedBitmap))
                            {
                                resizeGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                resizeGraphics.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
                            }

                            resizedBitmap.Save(memoryStream, ImageFormat.Jpeg);
                            memoryStream.Position = 0;
                            byte[] data = memoryStream.ToArray();
                            return ImageSource.FromStream(() => new MemoryStream(data));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error capturing screen for preview: {ex.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        public Task StartScreenCapture(CancellationToken cancellationToken, string actionName)
        {
            return Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    CaptureScreens(actionName);
                    Thread.Sleep(CaptureIntervalMs); // Capture at the reduced interval
                }
            }, cancellationToken);
        }

        public Task StartWebcamCapture(CancellationToken cancellationToken, string actionName, int intelligenceIntervalMs = 1000)
        {
            Debug.WriteLine("[ScreenCaptureService] StartWebcamCapture called");
            Console.WriteLine("[WEBCAM SAVE] StartWebcamCapture called");
            if (!string.IsNullOrEmpty(actionName))
            {
                Debug.Print("[ScreenCaptureService] Starting webcam capture with action or user input");
                Console.WriteLine($"[WEBCAM SAVE] Starting webcam capture with actionName: '{actionName}'");
            }
            else
            {
                Debug.Print("Webcam capture started without action or user input");
                Console.WriteLine("[WEBCAM SAVE] Starting webcam capture without actionName");
            }

            return Task.Run(() =>
            {
                Debug.WriteLine("[ScreenCaptureService] Webcam capture task started");
                Console.WriteLine("[WEBCAM SAVE] Webcam capture task started - using SHARED VideoCapture to prevent conflicts");

                // CRITICAL FIX: Use shared webcam instance to prevent multiple access conflicts
                lock (_webcamLock)
                {
                    if (_sharedWebcamCapture == null || !_sharedWebcamCapture.IsOpened())
                    {
                        Console.WriteLine("[WEBCAM SAVE] Initializing shared webcam capture...");
                        _sharedWebcamCapture?.Dispose();
                        _sharedWebcamCapture = new VideoCapture(0);
                        _isWebcamInitialized = _sharedWebcamCapture.IsOpened();

                        if (!_isWebcamInitialized)
                        {
                            Console.WriteLine("[WEBCAM SAVE] FAILED to initialize shared webcam!");
                            Debug.Print("Failed to open shared webcam.");
                            return;
                        }
                        else
                        {
                            Console.WriteLine("[WEBCAM SAVE] Shared webcam initialized successfully.");
                            Debug.Print("Shared webcam opened successfully.");
                        }
                    }

                    _webcamUserCount++;
                    Console.WriteLine($"[WEBCAM SAVE] Webcam user count: {_webcamUserCount}");
                }

                using var frame = new Mat();

                // Test initial frame read
                lock (_webcamLock)
                {
                    if (!_sharedWebcamCapture.Read(frame) || frame.Empty())
                    {
                        Console.WriteLine("[WEBCAM SAVE] FAILED to read initial frame from shared webcam.");
                        Debug.Print("Failed to read initial frame from shared webcam.");
                        _webcamUserCount--;
                        return;
                    }
                    else
                    {
                        Console.WriteLine("[WEBCAM SAVE] Initial frame read successfully from shared webcam.");
                        Debug.Print("Initial webcam frame read successfully.");
                    }
                }

                Debug.WriteLine($"[ScreenCaptureService] Webcam capture loop - CancellationToken.IsCancellationRequested: {cancellationToken.IsCancellationRequested}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Debug.Print("[ScreenCaptureService] Webcam capture loop - Attempting to read frame");

                        bool frameReadSuccess = false;
                        lock (_webcamLock)
                        {
                            if (_sharedWebcamCapture != null && _sharedWebcamCapture.IsOpened())
                            {
                                frameReadSuccess = _sharedWebcamCapture.Read(frame);
                            }
                        }

                        if (!frameReadSuccess)
                        {
                            Console.WriteLine("[WEBCAM SAVE] Failed to read frame from shared webcam.");
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
                            // Debug.Print("Webcam frame captured successfully.");
                        }

                        if (frame.Empty())
                        {
                            Debug.Print("Webcam frame is empty.");
                            continue;
                        }

                        // Only save webcam images when intelligence/recording is actually active (actionName provided)
                        if (!string.IsNullOrEmpty(actionName) && !actionName.StartsWith("TEST"))
                        {
                            string effectiveActionName = actionName;
                            string filePath = Path.Combine(_webcamImagesDirectory, $"WebcamImage_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

                            try
                            {
                                // Debug.WriteLine($"[ScreenCaptureService] Saving webcam image for active session: {filePath}");
                                // Console.WriteLine($"[WEBCAM SAVE] Attempting to save webcam image to: {filePath}");
                                // Debug.WriteLine($"[ScreenCaptureService] ActionName: '{actionName}', Effective actionName: '{effectiveActionName}'");
                                // Debug.WriteLine($"[ScreenCaptureService] Frame info - Width: {frame.Width}, Height: {frame.Height}, Empty: {frame.Empty()}, Type: {frame.Type()}");

                                // Ensure directory exists before saving
                                if (!Directory.Exists(_webcamImagesDirectory))
                                {
                                    Directory.CreateDirectory(_webcamImagesDirectory);
                                    // Debug.WriteLine($"[ScreenCaptureService] Created directory: {_webcamImagesDirectory}");
                                    // Console.WriteLine($"[WEBCAM SAVE] Created directory: {_webcamImagesDirectory}");
                                }

                                // Save the image with more detailed error checking
                                bool saveResult = Cv2.ImWrite(filePath, frame);
                                // Debug.WriteLine($"[ScreenCaptureService] ImWrite result: {saveResult}");
                                // Console.WriteLine($"[WEBCAM SAVE] ImWrite result: {saveResult}");

                                // Verify file was actually created and has content
                                if (saveResult && File.Exists(filePath))
                                {
                                    var fileInfo = new FileInfo(filePath);
                                    // Debug.WriteLine($"[ScreenCaptureService] SUCCESS! Webcam image saved: {filePath} (Size: {fileInfo.Length} bytes)");
                                    // Console.WriteLine($"[WEBCAM SAVE] SUCCESS! Webcam image saved: {filePath} (Size: {fileInfo.Length} bytes)");
                                    FileCaptured?.Invoke(filePath);
                                }
                                else
                                {
                                    Debug.WriteLine($"[ScreenCaptureService] FAILED to save webcam image!");
                                    Console.WriteLine($"[WEBCAM SAVE] FAILED to save webcam image to: {filePath}");
                                    Debug.WriteLine($"[ScreenCaptureService] SaveResult: {saveResult}, FileExists: {File.Exists(filePath)}");
                                    Console.WriteLine($"[WEBCAM SAVE] SaveResult: {saveResult}, FileExists: {File.Exists(filePath)}");
                                    Debug.WriteLine($"[ScreenCaptureService] Directory writable: {IsDirectoryWritable(_webcamImagesDirectory)}");
                                    Console.WriteLine($"[WEBCAM SAVE] Directory writable: {IsDirectoryWritable(_webcamImagesDirectory)}");
                                }

                                // Convert the captured frame to ImageSource for preview
                                ImageSource webcamImage = ConvertMatToImageSource(frame);
                                WebcamPreviewFrameReady?.Invoke(webcamImage);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ScreenCaptureService] EXCEPTION saving webcam image: {ex.Message}");
                                Console.WriteLine($"[WEBCAM SAVE] EXCEPTION saving webcam image: {ex.Message}");
                                Debug.WriteLine($"[ScreenCaptureService] Exception details: {ex}");
                                Debug.WriteLine($"[ScreenCaptureService] Stack trace: {ex.StackTrace}");
                            }
                        }
                        else
                        {
                            // Still provide preview frames even when not saving
                            ImageSource webcamImage = ConvertMatToImageSource(frame);
                            WebcamPreviewFrameReady?.Invoke(webcamImage);
                            Console.WriteLine($"[WEBCAM SAVE] Skipping save - no active intelligence session (actionName: '{actionName ?? "null"}')");
                        }

                        Thread.Sleep(intelligenceIntervalMs); // Use intelligence interval instead of fixed interval
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"Exception in webcam capture loop: {ex.Message}");
                        Console.WriteLine($"[WEBCAM SAVE] Exception in webcam capture loop: {ex.Message}");
                    }
                }

                // Cleanup when cancellation is requested
                lock (_webcamLock)
                {
                    _webcamUserCount--;
                    Console.WriteLine($"[WEBCAM SAVE] Webcam user count decreased to: {_webcamUserCount}");
                    if (_webcamUserCount <= 0)
                    {
                        Console.WriteLine("[WEBCAM SAVE] Disposing shared webcam capture...");
                        _sharedWebcamCapture?.Dispose();
                        _sharedWebcamCapture = null;
                        _isWebcamInitialized = false;
                        _webcamUserCount = 0;
                    }
                }

                Debug.Print("Webcam capture stopped by cancellation request.");
                Console.WriteLine("[WEBCAM SAVE] Webcam capture stopped by cancellation request.");
            }, cancellationToken);
        }
        public void StartPreviewMode()
        {
            _previewModeActive = true;
            IsPreviewEnabled = true; // Set this property to true
            _previewCts = new CancellationTokenSource();

            // Start sending preview frames
            Task.Run(() => GeneratePreviewFrames(_previewCts.Token));

            Debug.Print("Screen capture preview mode started");
        }

        public void StopPreviewMode()
        {
            _previewModeActive = false;
            IsPreviewEnabled = false; // Set this property to false
            Debug.WriteLine("[ScreenCaptureService] StopPreviewMode - Calling _previewCts?.Cancel()");
            _previewCts?.Cancel();
            _previewCts = null;

            Debug.Print("Screen capture preview mode stopped");
        }

        private async Task GeneratePreviewFrames(CancellationToken token)
        {
            try
            {
                Console.WriteLine("[WEBCAM SAVE] GeneratePreviewFrames - Using SHARED webcam to prevent conflicts");

                // CRITICAL FIX: Use shared webcam instance instead of creating a new one
                lock (_webcamLock)
                {
                    if (_sharedWebcamCapture == null || !_sharedWebcamCapture.IsOpened())
                    {
                        Console.WriteLine("[WEBCAM SAVE] GeneratePreviewFrames - Initializing shared webcam for preview...");
                        _sharedWebcamCapture?.Dispose();
                        _sharedWebcamCapture = new VideoCapture(0);
                        _isWebcamInitialized = _sharedWebcamCapture.IsOpened();

                        if (!_isWebcamInitialized)
                        {
                            Console.WriteLine("[WEBCAM SAVE] GeneratePreviewFrames - FAILED to initialize shared webcam for preview!");
                            Debug.Print("Failed to open webcam for preview.");
                            return;
                        }
                        else
                        {
                            Console.WriteLine("[WEBCAM SAVE] GeneratePreviewFrames - Shared webcam initialized successfully for preview.");
                            Debug.Print("Webcam opened successfully for preview.");
                        }
                    }

                    _webcamUserCount++;
                    Console.WriteLine($"[WEBCAM SAVE] GeneratePreviewFrames - Webcam user count: {_webcamUserCount}");
                }

                using var webcamFrame = new Mat();

                while (!token.IsCancellationRequested && _previewModeActive)
                {
                    // Check if preview is enabled before capturing frames
                    if (!_previewModeActive || !IsPreviewEnabled)
                    {
                        await Task.Delay(200, token); // Short delay if preview is not enabled
                        continue;
                    }

                    try
                    {
                        // 1. Capture screen preview
                        var screenImage = CaptureScreenForPreview();
                        if (screenImage != null)
                        {
                            ScreenPreviewFrameReady?.Invoke(screenImage);
                        }

                        // 2. Get the webcam preview using shared instance
                        bool frameReadSuccess = false;
                        lock (_webcamLock)
                        {
                            if (_sharedWebcamCapture != null && _sharedWebcamCapture.IsOpened())
                            {
                                // Debug.Print("[ScreenCaptureService] GeneratePreviewFrames - Attempting to read webcam frame");
                                frameReadSuccess = _sharedWebcamCapture.Read(webcamFrame) && !webcamFrame.Empty();
                            }
                        }

                        if (frameReadSuccess)
                        {
                            // Debug.WriteLine("[ScreenCaptureService] GeneratePreviewFrames - Webcam frame read successfully");
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

                // Cleanup when preview stops
                lock (_webcamLock)
                {
                    _webcamUserCount--;
                    Console.WriteLine($"[WEBCAM SAVE] GeneratePreviewFrames - Webcam user count decreased to: {_webcamUserCount}");
                    if (_webcamUserCount <= 0)
                    {
                        Console.WriteLine("[WEBCAM SAVE] GeneratePreviewFrames - Disposing shared webcam capture...");
                        _sharedWebcamCapture?.Dispose();
                        _sharedWebcamCapture = null;
                        _isWebcamInitialized = false;
                        _webcamUserCount = 0;
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

        private ImageSource ConvertMatToImageSource(Mat frame)
        {
            try
            {
                // Debug.Print("[ScreenCaptureService] ConvertMatToImageSource called");

                // Calculate aspect ratio to maintain proportions
                double aspectRatio = (double)frame.Width / frame.Height;
                int targetHeight = 240;
                int targetWidth = (int)(targetHeight * aspectRatio);

                // Resize the frame for better performance while maintaining aspect ratio
                using var resizedFrame = new Mat();
                Cv2.Resize(frame, resizedFrame, new OpenCvSharp.Size(targetWidth, targetHeight));

                // Convert the resized frame to a byte array
                byte[] imageBytes = resizedFrame.ToBytes(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                // Debug.Print($"[ScreenCaptureService] ConvertMatToImageSource - Image converted to byte array, size: {imageBytes.Length}");

                // Create an ImageSource from the byte array using a proper stream factory
                ImageSource imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                // Debug.Print($"[ScreenCaptureService] ConvertMatToImageSource - ImageSource created from MemoryStream");

                return imageSource;
            }
            catch (Exception ex)
            {
                Debug.Print($"Error converting webcam frame: {ex.Message}");
                return null;
            }
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

        /// <summary>
        /// Check if a directory is writable by attempting to create a temporary file
        /// </summary>
        private bool IsDirectoryWritable(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return false;

                string tempFile = Path.Combine(directoryPath, Guid.NewGuid().ToString() + ".tmp");
                File.WriteAllText(tempFile, "test");
                File.Delete(tempFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
