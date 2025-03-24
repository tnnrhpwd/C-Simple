using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
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
        public event Action<string> DebugMessageLogged;
        public event Action<string> FileCaptured;
        public event Action<ImageSource> ScreenPreviewFrameReady;
        public event Action<ImageSource> WebcamPreviewFrameReady;
        #endregion

        #region Properties
        private string _screenshotsDirectory;
        private string _webcamImagesDirectory;
        private bool _previewModeActive = false;
        private CancellationTokenSource _previewCts;
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

        private const int HORZRES = 8;
        private const int VERTRES = 10;
        private const int SRCCOPY = 0x00CC0020;

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
                    using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (var g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                        }

                        string filePath = Path.Combine(_screenshotsDirectory, $"ScreenCapture_{captureTime:yyyyMMdd_HHmmss_fff}_{screen.DeviceName.Replace("\\", "").Replace(":", "")}.png");
                        bitmap.Save(filePath, ImageFormat.Png);

                        LogDebug($"Screenshot saved to: {filePath}");
                        FileCaptured?.Invoke(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error capturing screen: {ex.Message}");
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
                    Thread.Sleep(1000); // Capture every second

                    if (_previewModeActive && ScreenPreviewFrameReady != null)
                    {
                        // In a real implementation, you would send preview frames periodically
                        // This is just a placeholder
                    }
                }
            }, cancellationToken);
        }

        public Task StartWebcamCapture(CancellationToken cancellationToken, string actionName, string userTouchInputText)
        {
            return Task.Run(() =>
            {
                using var capture = new VideoCapture(0);
                using var frame = new Mat();

                if (!capture.IsOpened())
                {
                    LogDebug("Failed to open webcam.");
                    return;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    capture.Read(frame);

                    if (frame.Empty())
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(userTouchInputText))
                    {
                        string filePath = Path.Combine(_webcamImagesDirectory, $"WebcamImage_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                        Cv2.ImWrite(filePath, frame);
                        FileCaptured?.Invoke(filePath);
                        LogDebug($"Webcam image saved to: {filePath}");
                    }
                    Thread.Sleep(1000);

                    if (_previewModeActive && WebcamPreviewFrameReady != null)
                    {
                        // In a real implementation, you would send preview frames periodically
                        // This is just a placeholder
                    }
                }
            }, cancellationToken);
        }

        public void StartPreviewMode()
        {
            _previewModeActive = true;
            _previewCts = new CancellationTokenSource();

            // Start sending preview frames
            Task.Run(() => GeneratePreviewFrames(_previewCts.Token));

            DebugMessageLogged?.Invoke("Screen capture preview mode started");
        }

        public void StopPreviewMode()
        {
            _previewModeActive = false;
            _previewCts?.Cancel();
            _previewCts = null;

            DebugMessageLogged?.Invoke("Screen capture preview mode stopped");
        }

        private async Task GeneratePreviewFrames(CancellationToken token)
        {
            try
            {
                // Get real-time screen and webcam captures
                using var webcamCapture = new VideoCapture(0);
                using var webcamFrame = new Mat();

                if (!webcamCapture.IsOpened())
                {
                    LogDebug("Failed to open webcam for preview.");
                }

                while (!token.IsCancellationRequested && _previewModeActive)
                {
                    try
                    {
                        // Capture and send screen frame
                        if (ScreenPreviewFrameReady != null)
                        {
                            // Capture actual screen image
                            var screenImage = CaptureScreenForPreview();
                            if (screenImage != null)
                            {
                                ScreenPreviewFrameReady.Invoke(screenImage);
                            }
                        }

                        // Capture and send webcam frame
                        if (WebcamPreviewFrameReady != null && webcamCapture.IsOpened())
                        {
                            if (webcamCapture.Read(webcamFrame) && !webcamFrame.Empty())
                            {
                                var webcamImage = ConvertMatToImageSource(webcamFrame);
                                if (webcamImage != null)
                                {
                                    WebcamPreviewFrameReady.Invoke(webcamImage);
                                }
                            }
                        }

                        // Delay between frames
                        await Task.Delay(100, token); // Faster update rate for smoother preview
                    }
                    catch (Exception ex)
                    {
                        DebugMessageLogged?.Invoke($"Error capturing preview frame: {ex.Message}");
                        await Task.Delay(1000, token); // Longer delay on error
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                DebugMessageLogged?.Invoke($"Error in preview frame generation: {ex.Message}");
            }
        }

        private ImageSource CaptureScreenForPreview()
        {
#if WINDOWS
            try
            {
                // Capture primary screen
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                    }

                    // Convert to stream and create image source
                    using (var memoryStream = new MemoryStream())
                    {
                        bitmap.Save(memoryStream, ImageFormat.Png);
                        memoryStream.Position = 0;

                        // Return as StreamImageSource
                        var streamImageSource = ImageSource.FromStream(() => new MemoryStream(memoryStream.ToArray()));
                        return streamImageSource;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error capturing screen for preview: {ex.Message}");
            }
#endif
            return null;
        }

        private ImageSource ConvertMatToImageSource(Mat frame)
        {
            try
            {
                // Save to temporary file and load as image source
                string tempFile = Path.Combine(Path.GetTempPath(), $"webcam_preview_{Guid.NewGuid()}.jpg");
                Cv2.ImWrite(tempFile, frame);

                // Create image source from file
                var imageSource = ImageSource.FromFile(tempFile);

                // Schedule file deletion after a delay
                Task.Run(async () =>
                {
                    await Task.Delay(5000); // Delete after 5 seconds
                    try { File.Delete(tempFile); } catch { }
                });

                return imageSource;
            }
            catch (Exception ex)
            {
                LogDebug($"Error converting Mat to ImageSource: {ex.Message}");
                return null;
            }
        }

        private void LogDebug(string message)
        {
            DebugMessageLogged?.Invoke(message);
        }
    }
}
