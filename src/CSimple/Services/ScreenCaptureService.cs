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

namespace CSimple.Services
{
    public class ScreenCaptureService
    {
        #region Events
        public event Action<string> DebugMessageLogged;
        public event Action<string> FileCaptured;
        #endregion

        #region Properties
        private string _screenshotsDirectory;
        private string _webcamImagesDirectory;
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
                }
            }, cancellationToken);
        }

        private void LogDebug(string message)
        {
            DebugMessageLogged?.Invoke(message);
        }
    }
}
