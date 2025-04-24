using CSimple.Pages;
using CSimple.Services;
using CSimple.ViewModels; // Add ViewModels namespace
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
            // Add the namespace here
            .ConfigureMauiAppWithBehaviors()
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
        // --- Register ViewModels ---
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<NetPageViewModel>(); // Register NetPageViewModel
        services.AddSingleton<OrientPageViewModel>(); // Register OrientPageViewModel

        // --- Register Pages ---
        services.AddSingleton<HomePage>();
        services.AddSingleton<LoginPage>();
        services.AddSingleton<SettingsPage>();
        // Register NetPage with ViewModel dependency
        services.AddSingleton<NetPage>(); // Inject ViewModel automatically
        services.AddSingleton<OrientPage>(); // Register OrientPage

        // --- Register Services ---
        services.AddSingleton<DataService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<FileService>();  // Register FileService
        services.AddSingleton<GoalService>();  // GoalService depends on FileService
        services.AddSingleton<ActionService>();  // Register ActionService with DI
        services.AddSingleton<GlobalInputCapture>();
        services.AddSingleton<IOnTrainModelClickedService, OnTrainModelClickedService>();
        services.AddSingleton<PythonBootstrapper>();  // Replace PythonDependencyManager with PythonBootstrapper
        services.AddSingleton<HuggingFaceService>(); // Add HuggingFace service
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

        // Register VoiceAssistantService
        services.AddSingleton<VoiceAssistantService>();

        // Update HomePage registration to include VoiceAssistantService
        services.AddSingleton(sp => new HomePage(
            sp.GetRequiredService<HomeViewModel>(),
            sp.GetRequiredService<DataService>(),
            sp.GetRequiredService<AppModeService>(),
            sp.GetRequiredService<VoiceAssistantService>()
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

// Add extension method for configuring behaviors
public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder ConfigureMauiAppWithBehaviors(this MauiAppBuilder builder)
    {
        // Make sure behaviors are included in the assembly
        builder.ConfigureEffects(effects =>
        {
            // This is just to ensure the behaviors assembly is loaded
            var behaviorType = typeof(CSimple.Behaviors.EnumBindingBehavior);
        });

        return builder;
    }
}