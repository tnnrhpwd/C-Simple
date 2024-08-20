using Microsoft.Maui.LifecycleEvents;
using CSimple.Pages;
using CSimple.ViewModels;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using Microsoft.UI;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls.Hosting;

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

            lifecycle.AddWindows(windows => windows.OnWindowCreated((window) => {
                // 'del.ExtendsContentIntoTitleBar = true;
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                        var appWindow = AppWindow.GetFromWindowId(windowId);
                        appWindow.Resize(new SizeInt32(800, 900));
            }));
            #endif
        });

        var services = builder.Services;
        #if WINDOWS
            services.AddSingleton<ITrayService, WinUI.TrayService>();
            services.AddSingleton<INotificationService, WinUI.NotificationService>();
        #elif MACCATALYST
            services.AddSingleton<ITrayService, MacCatalyst.TrayService>();
            services.AddSingleton<INotificationService, MacCatalyst.NotificationService>();
        #endif
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<HomePage>();

        return builder.Build();
    }
}