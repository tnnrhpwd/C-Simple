using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using System;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Native;

#if WINDOWS
using Microsoft.Maui.Dispatching;
#endif

namespace YourNamespace.Services
{
    public class MouseTrackingService
    {
        [DllImport("user32.dll")]
        private static extern bool GetRawInputData(IntPtr hRawInput, uint uiCommand, out RawInput pData, ref uint pDataSize, uint uiSizeHeader);

        private const uint RIM_TYPEMOUSE = 0;
        private const uint RID_INPUT = 0x10000003;

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInput
        {
            public uint HeaderSize;
            public uint Type;
            public uint MouseFlags;
            public uint MouseData;
            public int LastX;
            public int LastY;
            public uint ButtonData;
            public uint ExtraInformation;
        }
        private bool _isTracking;
        private Point _lastPosition;

        public List<Point> MouseMovements { get; private set; } = new List<Point>();

        public event Action<Point> MouseMoved;

        public MouseTrackingService()
        {
            _lastPosition = new Point(0, 0);
        }

        public void StartTracking()
        {
            _isTracking = true;
            _ = TrackMouseMovementAsync();
            uint size = 0;
            GetRawInputData(IntPtr.Zero, RID_INPUT, out RawInput rawInput, ref size, (uint)Marshal.SizeOf(typeof(RawInput)));

            // Process the raw input data
            Console.WriteLine($"Mouse X: {rawInput.LastX}, Mouse Y: {rawInput.LastY}");
        }

        public void StopTracking()
        {
            _isTracking = false;
        }

        private async Task TrackMouseMovementAsync()
        {
            while (_isTracking)
            {
                var currentPosition = await GetMousePositionAsync();
                if (currentPosition != _lastPosition)
                {
                    var delta = new Point(currentPosition.X - _lastPosition.X, currentPosition.Y - _lastPosition.Y);
                    MouseMovements.Add(delta);
                    MouseMoved?.Invoke(delta);
                    _lastPosition = currentPosition;
                }
                await Task.Delay(16); // approximately 60 frames per second
            }
        }

        private Task<Point> GetMousePositionAsync()
        {
            // Platform-specific code to get mouse position
#if WINDOWS
            var position = WindowsMouseHelper.GetMousePosition();
            return Task.FromResult(new Point(position.X, position.Y));
#else
            return Task.FromResult(new Point(0, 0)); // Placeholder for non-Windows platforms
#endif
        }
    }

#if WINDOWS
    public static class WindowsMouseHelper
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static POINT GetMousePosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }
    }

    public struct POINT
    {
        public int X;
        public int Y;
    }
#endif
}
