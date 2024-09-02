using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
#if WINDOWS
using Microsoft.Maui.Dispatching;
#endif

namespace YourNamespace.Services
{
    public class MouseTrackingService
    {
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
