namespace CSimple;

public static class WindowExtensions
{
    public static IntPtr Hwnd { get; set; }

    public static void SetIcon(string iconFilename)
    {
        if (Hwnd == IntPtr.Zero)
            return;

        var hIcon = PInvoke.User32.LoadImage(IntPtr.Zero, iconFilename,
           PInvoke.User32.ImageType.IMAGE_ICON, 16, 16, PInvoke.User32.LoadImageFlags.LR_LOADFROMFILE);

        PInvoke.User32.SendMessage(Hwnd, PInvoke.User32.WindowMessage.WM_SETICON, (IntPtr)0, hIcon);
    }

    public static void BringToFront()
    {
        if (Hwnd == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("BringToFront: Hwnd is Zero - window handle not set");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"BringToFront: Attempting to bring window to front with handle {Hwnd}");

        PInvoke.User32.ShowWindow(Hwnd, PInvoke.User32.WindowShowStyle.SW_SHOW);
        PInvoke.User32.ShowWindow(Hwnd, PInvoke.User32.WindowShowStyle.SW_RESTORE);

        var result = PInvoke.User32.SetForegroundWindow(Hwnd);
        System.Diagnostics.Debug.WriteLine($"BringToFront: SetForegroundWindow result: {result}");
    }

    public static void MinimizeToTray()
    {
        PInvoke.User32.ShowWindow(Hwnd, PInvoke.User32.WindowShowStyle.SW_MINIMIZE);
        PInvoke.User32.ShowWindow(Hwnd, PInvoke.User32.WindowShowStyle.SW_HIDE);
    }
}
