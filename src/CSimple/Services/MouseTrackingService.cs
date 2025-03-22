using Microsoft.Maui.Graphics;
using System.Runtime.InteropServices;

#if WINDOWS
#endif

namespace CSimple.Services
{
    public class MouseTrackingService
    {
        public event Action<Point> MouseMoved;

        public void StartTracking(IntPtr hwnd)
        {
            // Implement mouse tracking logic here
        }

        public void StopTracking()
        {
            // Implement logic to stop mouse tracking here
        }

        private void OnMouseMoved(Point delta)
        {
            MouseMoved?.Invoke(delta);
        }
    }

#if WINDOWS
    public static class WindowsMouseHelper
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static POINT GetMousePosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }
    }
#endif
}
