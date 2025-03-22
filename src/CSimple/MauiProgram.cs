using CSimple.Pages;
using CSimple.ViewModels;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.UI;
namespace CSimple;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(static fonts =>
            {
                fonts.AddFont("fa-solid-900.ttf", "FontAwesome");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
            });
        builder.ConfigureLifecycleEvents(lifecycle =>
            {
#if WINDOWS
                lifecycle.AddWindows(windows => windows.OnWindowCreated((window) =>
                {
                    // Get the window handle
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = AppWindow.GetFromWindowId(windowId);

                    // Set window size
                    appWindow.Resize(new SizeInt32(800, 900));

                    // Configure title bar to show the app name
                    var titleBar = appWindow.TitleBar;
                    titleBar.ExtendsContentIntoTitleBar = false;

                    // Set window title (app name)
                    window.Title = "CSimple";

                    // Match button colors with the title bar background (using dark theme color)
                    // The Background_Mid color from the theme is approximately #1E1E1E
                    Color darkBackground = Color.FromArgb(255, 8, 27, 37); // Matching Background_Mid from theme

                    // Set title bar button colors
                    titleBar.BackgroundColor = darkBackground;
                    titleBar.ButtonBackgroundColor = darkBackground;
                    titleBar.ButtonInactiveBackgroundColor = darkBackground;
                    titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 30, 50, 60); // Slightly lighter on hover
                    titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 40, 60, 70); // Slightly lighter when pressed

                    // Button foreground colors
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.ButtonHoverForegroundColor = Colors.White;
                    titleBar.ButtonPressedForegroundColor = Colors.LightGray;
                    titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 180, 180, 180);

                    // Inactive state colors
                    titleBar.InactiveBackgroundColor = darkBackground;
                }));
#endif
            });

        var services = builder.Services;
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<HomePage>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<LoginPage>();
        services.AddSingleton<NetPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<DataService>();
        services.AddSingleton<GlobalInputCapture>();

#if WINDOWS
        services.AddSingleton<ITrayService, WinUI.TrayService>();
        services.AddSingleton<INotificationService, WinUI.NotificationService>();
#elif MACCATALYST
        services.AddSingleton<ITrayService, MacCatalyst.TrayService>();
        services.AddSingleton<INotificationService, MacCatalyst.NotificationService>();
#endif

        services.AddTransient<App>();

        return builder.Build();
    }
}