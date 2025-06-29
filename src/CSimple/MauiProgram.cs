using CSimple.Pages;
using CSimple.Services;
using CSimple.ViewModels;

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
            });

        var services = builder.Services;

        services.AddSingleton<DataService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<FileService>();
        services.AddSingleton<GoalService>();
        services.AddSingleton<ActionService>();
        services.AddSingleton<GlobalInputCapture>();
        services.AddSingleton<IOnTrainModelClickedService, OnTrainModelClickedService>();
        services.AddSingleton<PythonBootstrapper>();
        services.AddSingleton<HuggingFaceService>();
        services.AddSingleton<NodeManagementService>(); // ADDED
        services.AddSingleton<PipelineManagementService>(); // ADDED
        services.AddSingleton(sp =>
        {
            var actionService = sp.GetRequiredService<ActionService>();
            return new InputCaptureService(actionService);
        }); services.AddSingleton<AppModeService>();
        services.AddSingleton<ActionGroupService>();
        services.AddSingleton<GameSettingsService>();
        services.AddSingleton<ActionGroupCopierService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ScreenCaptureService>();
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<ObserveDataService>();
        services.AddSingleton<MouseTrackingService>();
        services.AddSingleton<VoiceAssistantService>();

        // Add the refactored services
        services.AddSingleton<PythonEnvironmentService>();
        services.AddSingleton<ModelCommunicationService>();
        services.AddSingleton<ModelExecutionService>();
        services.AddSingleton<ModelImportExportService>();
        services.AddSingleton<IModelDownloadService, ModelDownloadService>();
        services.AddSingleton<IModelImportService, ModelImportService>();
        services.AddSingleton<IChatManagementService, ChatManagementService>();
        services.AddSingleton<IMediaSelectionService, MediaSelectionService>();

        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<NetPageViewModel>();
        services.AddSingleton<OrientPageViewModel>();

        services.AddSingleton<ActionPageViewModel>();

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
        services.AddSingleton<GoalPage>();
        services.AddSingleton<ActionPage>();
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

        services.AddSingleton<App>();

        return builder.Build();
    }
}

public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder ConfigureMauiAppWithBehaviors(this MauiAppBuilder builder)
    {
        builder.ConfigureEffects(effects =>
        {
            var behaviorType = typeof(CSimple.Behaviors.EnumBindingBehavior);
        });

        return builder;
    }
}