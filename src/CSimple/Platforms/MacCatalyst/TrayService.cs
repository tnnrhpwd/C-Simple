using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;
using CSimple.Services;

namespace CSimple.MacCatalyst;

public class TrayService : NSObject, ITrayService
{
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr IntPtr_objc_msgSend_nfloat(IntPtr receiver, IntPtr selector, nfloat arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    public static extern void void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    public static extern void void_objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool arg1);

    NSObject systemStatusBarObj;
    NSObject statusBarObj;
    NSObject statusBarItem;
    NSObject statusBarButton;
    NSObject statusBarImage;

    public Action ClickHandler { get; set; }
    public Action StartListenHandler { get; set; }
    public Action StopListenHandler { get; set; }
    public Action ShowSettingsHandler { get; set; }
    public Action QuitApplicationHandler { get; set; }
    public Func<bool> IsListeningCallback { get; set; }

    public void Initialize()
    {
        statusBarObj = Runtime.GetNSObject(Class.GetHandle("NSStatusBar"));
        systemStatusBarObj = statusBarObj.PerformSelector(new Selector("systemStatusBar"));
        statusBarItem = Runtime.GetNSObject(IntPtr_objc_msgSend_nfloat(systemStatusBarObj.Handle, Selector.GetHandle("statusItemWithLength:"), -1));
        statusBarButton = Runtime.GetNSObject(IntPtr_objc_msgSend(statusBarItem.Handle, Selector.GetHandle("button")));
        statusBarImage = Runtime.GetNSObject(IntPtr_objc_msgSend(ObjCRuntime.Class.GetHandle("NSImage"), Selector.GetHandle("alloc")));

        var imgPath = System.IO.Path.Combine(NSBundle.MainBundle.BundlePath, "Contents", "Resources", "Platforms", "MacCatalyst", "trayicon.png");
        var imageFileStr = NSString.CreateNative(imgPath);
        var nsImagePtr = IntPtr_objc_msgSend_IntPtr(statusBarImage.Handle, Selector.GetHandle("initWithContentsOfFile:"), imageFileStr);

        void_objc_msgSend_IntPtr(statusBarButton.Handle, Selector.GetHandle("setImage:"), statusBarImage.Handle);
        void_objc_msgSend_bool(nsImagePtr, Selector.GetHandle("setTemplate:"), true);

        // Handle click
        void_objc_msgSend_IntPtr(statusBarButton.Handle, Selector.GetHandle("setTarget:"), this.Handle);
        void_objc_msgSend_IntPtr(statusBarButton.Handle, Selector.GetHandle("setAction:"), new Selector("handleButtonClick:").Handle);

        // Note: macOS context menu implementation would require additional NSMenu setup
        // For now, just basic click support is implemented
    }

    [Export("handleButtonClick:")]
    void HandleClick(NSObject senderStatusBarButton)
    {
        var nsapp = Runtime.GetNSObject(Class.GetHandle("NSApplication"));
        var sharedApp = nsapp.PerformSelector(new Selector("sharedApplication"));

        void_objc_msgSend_bool(sharedApp.Handle, Selector.GetHandle("activateIgnoringOtherApps:"), true);

        ClickHandler?.Invoke();
    }

    // Progress notification methods - basic implementation for macOS
    public void ShowProgress(string title, string message, double progress)
    {
        // Basic implementation - could be enhanced with native macOS notifications
        System.Diagnostics.Debug.WriteLine($"macOS TrayService: {title} - {message} ({progress:P0})");
    }

    public void UpdateProgress(double progress, string message = null)
    {
        // Basic implementation
        System.Diagnostics.Debug.WriteLine($"macOS TrayService: Progress {progress:P0} - {message}");
    }

    public void HideProgress()
    {
        // Basic implementation
        System.Diagnostics.Debug.WriteLine("macOS TrayService: Hide progress");
    }

    public void ShowCompletionNotification(string title, string message)
    {
        // Basic implementation
        System.Diagnostics.Debug.WriteLine($"macOS TrayService: {title} - {message}");
    }
}
