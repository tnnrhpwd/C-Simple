using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CSimple.Input; // Add using for the new namespace

namespace CSimple
{
    public static class InputSimulator
    {
        #region Mouse Input Methods (Higher Level - Keep or Refactor)

        // This MoveMouse uses absolute coordinates and GetSystemMetrics.
        // It can be kept as a higher-level abstraction or refactored to use LowLevelInputSimulator.
        public static void MoveMouse(int x, int y)
        {
            // Option 1: Keep implementation using LowLevelInputSimulator methods
            LowLevelInputSimulator.SendLowLevelMouseMove(x, y);

            // Option 2: Keep original implementation if direct access to metrics is preferred here
            /*
            INPUT[] inputs = new INPUT[1]; // Need INPUT struct definition if kept
            inputs[0].type = INPUT_MOUSE;

            int screenWidth = LowLevelInputSimulator.GetSystemMetrics(SM_CXSCREEN); // Use LowLevelInputSimulator
            int screenHeight = LowLevelInputSimulator.GetSystemMetrics(SM_CYSCREEN); // Use LowLevelInputSimulator

            inputs[0].u.mi.dx = (x * 65535) / screenWidth;
            inputs[0].u.mi.dy = (y * 65535) / screenHeight;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE; // Need constants if kept
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo(); // Need P/Invoke if kept

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT))); // Need P/Invoke if kept
            */
        }

        // SmoothMouseMove uses MoveMouse, so it depends on the implementation above.
        public static async Task SmoothMouseMove(int startX, int startY, int endX, int endY, int steps = 20, int delayMs = 2)
        {
            if (startX < 0 || startY < 0)
            {
                LowLevelInputSimulator.GetCursorPos(out LowLevelInputSimulator.POINT currentPos); // Use LowLevelInputSimulator
                startX = currentPos.X;
                startY = currentPos.Y;
            }

            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            int adaptiveSteps = distance < 100 ? steps : (int)(steps * Math.Sqrt(distance / 100.0));
            adaptiveSteps = Math.Min(adaptiveSteps, 100);

            Random random = new Random();
            int randomOffsetX = random.Next(-10, 10);
            int randomOffsetY = random.Next(-10, 10);

            int controlX = (startX + endX) / 2 + randomOffsetX;
            int controlY = (startY + endY) / 2 + randomOffsetY;

            int prevX = startX;
            int prevY = startY;

            for (int i = 1; i <= adaptiveSteps; i++)
            {
                float t = (float)i / adaptiveSteps;

                float easedT = EaseHumanMove(t);

                float u = 1.0f - easedT;
                float tt = easedT * easedT;
                float uu = u * u;

                int x = (int)(uu * startX + 2 * u * easedT * controlX + tt * endX);
                int y = (int)(uu * startY + 2 * u * easedT * controlY + tt * endY);

                float shakeFactor = 4.0f * easedT * (1.0f - easedT);
                if (distance > 20)
                {
                    x += (int)(random.Next(-2, 3) * shakeFactor);
                    y += (int)(random.Next(-2, 3) * shakeFactor);
                }

                MoveMouse(x, y);

                double segmentDistance = Math.Sqrt(Math.Pow(x - prevX, 2) + Math.Pow(y - prevY, 2));
                prevX = x;
                prevY = y;

                int actualDelay = delayMs;
                if (i < adaptiveSteps * 0.2 || i > adaptiveSteps * 0.8)
                {
                    actualDelay = (int)(delayMs * 1.5);
                }
                else if (segmentDistance > 10)
                {
                    actualDelay = (int)(delayMs * 1.3);
                }

                if (actualDelay > 0)
                    await Task.Delay(actualDelay);
            }

            MoveMouse(endX, endY);
        }

        private static float EaseHumanMove(float t)
        {
            const float power = 2.5f;
            const float overshoot = 0.03f;

            if (t < 0.5f)
            {
                return 0.5f * (float)Math.Pow(t * 2, power);
            }
            else
            {
                float decel = 0.5f + 0.5f * (1 - (float)Math.Pow(2 - t * 2, power));

                if (t > 0.8f && t < 0.95f)
                    decel += overshoot * (t - 0.8f) * (0.95f - t) * 10;

                return decel;
            }
        }

        // SimulateMouseClick uses MoveMouse and sends click events. Refactor to use LowLevelInputSimulator.
        public static void SimulateMouseClick(Input.MouseButton button, int x, int y) // Use namespaced enum
        {
            MoveMouse(x, y); // Ensure position
            LowLevelInputSimulator.SendLowLevelMouseClick(button, false, x, y); // Send Down
            LowLevelInputSimulator.SendLowLevelMouseClick(button, true, x, y);  // Send Up
        }

        // SimulateMouseEvent sends only up or down events. Refactor to use LowLevelInputSimulator.
        public static void SimulateMouseEvent(Input.MouseButton button, bool isUp) // Use namespaced enum
        {
            LowLevelInputSimulator.GetCursorPos(out LowLevelInputSimulator.POINT currentPos); // Get current position
            LowLevelInputSimulator.SendLowLevelMouseClick(button, isUp, currentPos.X, currentPos.Y); // Send event at current pos
        }

        // SendRawMouseInput moved to LowLevelInputSimulator. Keep this wrapper?
        public static void SendRawMouseInput(int deltaX, int deltaY)
        {
            LowLevelInputSimulator.SendRawMouseInput(deltaX, deltaY);
        }
        #endregion

        #region Keyboard Input Methods (Higher Level - Keep or Refactor)

        // SimulateKeyDown refactored to use LowLevelInputSimulator
        public static void SimulateKeyDown(Input.VirtualKey key) // Use namespaced enum
        {
            LowLevelInputSimulator.SendKeyboardInput((ushort)key, false);
        }

        // SimulateKeyUp refactored to use LowLevelInputSimulator
        public static void SimulateKeyUp(Input.VirtualKey key) // Use namespaced enum
        {
            LowLevelInputSimulator.SendKeyboardInput((ushort)key, true);
        }

        // SimulateKeyPress uses KeyDown/KeyUp, no changes needed if they are refactored correctly.
        public static void SimulateKeyPress(Input.VirtualKey key) // Use namespaced enum
        {
            SimulateKeyDown(key);
            SimulateKeyUp(key);
        }
        #endregion

        #region Advanced Game Input Methods (Keep or Refactor)
        // These methods use SetCursorPos and GetCursorPos.
        // Keep them here if they represent a distinct "game-focused" simulation approach.
        // Ensure P/Invokes for SetCursorPos are available or move them to LowLevelInputSimulator too.

        private static bool _gameEnhancedMode = true;
        private static int _gameMouseSensitivity = 50;
        private static int _gameMovementSteps = 40;
        private static int _gameMovementDelay = 8;

        public static void SetGameEnhancedMode(bool enabled, int mouseSensitivity = 50)
        {
            _gameEnhancedMode = enabled;
            _gameMouseSensitivity = Math.Clamp(mouseSensitivity, 1, 200);
        }

        public static async Task GameMouseMove(int deltaX, int deltaY)
        {
            await GameMouseMove(deltaX, deltaY, _gameMovementSteps, _gameMovementDelay);
        }

        public static async Task GameMouseMove(int deltaX, int deltaY, int steps, int delayMs)
        {
            LowLevelInputSimulator.GetCursorPos(out LowLevelInputSimulator.POINT startPos); // Use LowLevelInputSimulator

            float sensitivityFactor = _gameMouseSensitivity / 100f;
            deltaX = (int)(deltaX * sensitivityFactor);
            deltaY = (int)(deltaY * sensitivityFactor);

            int targetX = startPos.X + deltaX;
            int targetY = startPos.Y + deltaY;

            await SmoothGameMouseMove(startPos.X, startPos.Y, targetX, targetY, steps, delayMs);
        }

        private static async Task SmoothGameMouseMove(int startX, int startY, int endX, int endY, int steps = 40, int delayMs = 8)
        {
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            Random random = new Random();

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;

                float easedT = 1 - (float)Math.Pow(1 - t, 3);

                int x = (int)(startX + (endX - startX) * easedT);
                int y = (int)(startY + (endY - startY) * easedT);

                if (i > 1 && i < steps - 1 && distance > 50)
                {
                    x += random.Next(-1, 2);
                    y += random.Next(-1, 2);
                }

                SetCursorPos(x, y); // Keep using SetCursorPos P/Invoke

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }

            SetCursorPos(endX, endY); // Keep using SetCursorPos P/Invoke
        }

        [DllImport("user32.dll")] // Keep SetCursorPos here if Game methods remain
        private static extern bool SetCursorPos(int X, int Y);

        #endregion

        // MoveDirectlyAsync uses MoveMouse, depends on its implementation.
        public static async Task MoveDirectlyAsync(int startX, int startY, int endX, int endY, int steps = 20)
        {
            int stepX = (endX - startX) / steps;
            int stepY = (endY - startY) / steps;

            for (int i = 0; i < steps; i++)
            {
                MoveMouse(startX + stepX * i, startY + stepY * i);
                await Task.Delay(10);
            }

            MoveMouse(endX, endY);
        }

        // Window management methods are not input simulation, keep them here.
        public static bool BringWindowToForeground(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            return SetForegroundWindow(hWnd);
        }

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;
    }
}
