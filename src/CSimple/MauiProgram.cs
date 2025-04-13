using CSimple.Pages;
using CSimple.Services;
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
            })
            .ConfigureMauiHandlers(handlers =>
            {
                // Add any handler configuration here
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                // Add Windows-specific customization for title bar
                events.AddWindows(windowsLifecycleBuilder =>
                {
                    windowsLifecycleBuilder.OnWindowCreated(window =>
                    {
                        window.ExtendsContentIntoTitleBar = true;

                        // Get the current theme colors
                        var app = Microsoft.Maui.Controls.Application.Current as App;
                        if (app != null)
                        {
                            // Force an update of the window colors
                            app.Dispatcher.Dispatch(() =>
                            {
                                // This will trigger the update of the title bar colors
                                Microsoft.Maui.Controls.Application.Current.UserAppTheme =
                                    Microsoft.Maui.Controls.Application.Current.UserAppTheme;
                            });
                        }
                    });
                });
#endif
            });

        // No need to register styles here - they are already included in App.xaml
        // Don't add resources to Application.Current here since it's not initialized yet

        builder.ConfigureLifecycleEvents(lifecycle =>
        {
#if WINDOWS
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
        services.AddSingleton<FileService>();  // Register FileService
        services.AddSingleton<GoalService>();  // GoalService depends on FileService
        services.AddSingleton<ActionService>();  // Register ActionService with DI
        services.AddSingleton<GlobalInputCapture>();
        services.AddSingleton<IOnTrainModelClickedService, OnTrainModelClickedService>();
        // Inject ActionService into InputCaptureService
        services.AddSingleton(sp =>
        {
            var actionService = sp.GetRequiredService<ActionService>();
            return new InputCaptureService(actionService);
        });
        services.AddSingleton<AppModeService>();
        services.AddSingleton<ActionGroupService>();
        services.AddSingleton<GameSettingsService>();
        services.AddSingleton<ActionGroupCopierService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ScreenCaptureService>();
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<ObserveDataService>();
        services.AddSingleton<MouseTrackingService>();

        // Register ObservePage with all dependencies
        services.AddSingleton(sp => new ObservePage(
            sp.GetRequiredService<InputCaptureService>(),
            sp.GetRequiredService<ScreenCaptureService>(),
            sp.GetRequiredService<AudioCaptureService>(),
            sp.GetRequiredService<ObserveDataService>(),
            sp.GetRequiredService<MouseTrackingService>(),
            sp.GetRequiredService<ActionService>()
        ));

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