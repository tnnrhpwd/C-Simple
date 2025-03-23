using CSimple.Pages;
using CSimple.ViewModels;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
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

                //lifecycle
                //    .AddWindows(windows =>
                //        windows.OnNativeMessage((app, args) => {
                //            if (WindowExtensions.Hwnd == IntPtr.Zero)
                //            {
                //                WindowExtensions.Hwnd = args.Hwnd;
                //                WindowExtensions.SetIcon("Platforms/Windows/trayicon.ico");
                //            }
                //        }));

                lifecycle.AddWindows(windows => windows.OnWindowCreated((window) =>
                {
                    // 'del.ExtendsContentIntoTitleBar = true;
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = AppWindow.GetFromWindowId(windowId);
                    appWindow.Resize(new SizeInt32(800, 900));
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
        services.AddSingleton<SettingsService>();
        services.AddSingleton<GoalService>();  // Add the new GoalService
        services.AddSingleton<GlobalInputCapture>();
        services.AddSingleton<IOnTrainModelClickedService, OnTrainModelClickedService>();

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