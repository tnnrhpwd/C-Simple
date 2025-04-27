using CSimple.Pages;
using CSimple.Services;
using CSimple.ViewModels; // Ensure ViewModels namespace is included

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
            .ConfigureMauiAppWithBehaviors()
            .ConfigureMauiHandlers(handlers =>
            {
                // Add any handler configuration here
            });

        var services = builder.Services;

        // --- Register Services (ensure FileService, HuggingFaceService, PythonBootstrapper are here) ---
        services.AddSingleton<DataService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<FileService>();  // Ensure registered
        services.AddSingleton<GoalService>();
        services.AddSingleton<ActionService>();
        services.AddSingleton<GlobalInputCapture>();
        services.AddSingleton<IOnTrainModelClickedService, OnTrainModelClickedService>();
        services.AddSingleton<PythonBootstrapper>(); // Ensure registered
        services.AddSingleton<HuggingFaceService>(); // Ensure registered
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
        services.AddSingleton<VoiceAssistantService>();

        // --- Register ViewModels ---
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<NetPageViewModel>();
        // Register OrientPageViewModel with dependencies
        services.AddSingleton<OrientPageViewModel>(); // Dependencies injected automatically if registered

        services.AddSingleton<ActionPageViewModel>();

        // --- Register Pages ---
        // Update HomePage registration
        services.AddSingleton(sp => new HomePage(
            sp.GetRequiredService<HomeViewModel>(),
            sp.GetRequiredService<DataService>(),
            sp.GetRequiredService<AppModeService>(),
            sp.GetRequiredService<VoiceAssistantService>()
        ));
        services.AddSingleton<LoginPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<NetPage>();
        services.AddSingleton<OrientPage>();
        // Register GoalPage with dependencies
        services.AddSingleton<GoalPage>(); // Dependencies injected automatically if registered
        services.AddSingleton<ActionPage>();
        // Register ObservePage with dependencies
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

        // Register App with dependencies
        services.AddSingleton<App>(); // Changed from Transient to Singleton if App holds state like NetPageViewModel

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