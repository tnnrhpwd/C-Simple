using CSimple.Pages;
using CSimple.Services;
using CSimple.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace CSimple.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCSimpleServices(this IServiceCollection services)
        {
            // Core Services
            services.AddSingleton<IAppPathService, AppPathService>();
            services.AddSingleton<DataService>();
            services.AddSingleton(sp => new SettingsService(sp.GetRequiredService<DataService>(), sp.GetRequiredService<IAppPathService>()));
            services.AddSingleton(sp => new FileService(sp.GetRequiredService<IAppPathService>()));
            services.AddSingleton<GoalService>();
            services.AddSingleton<ActionService>();
            services.AddSingleton<GlobalInputCapture>();
            services.AddSingleton<PythonBootstrapper>();
            services.AddSingleton<HuggingFaceService>();

            // Interface-based Services
            services.AddSingleton<IOnTrainModelClickedService, OnTrainModelClickedService>();
            services.AddSingleton<IMemoryCompressionService, MemoryCompressionService>();
            services.AddSingleton<ICameraOffsetService, CameraOffsetService>();
            services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            services.AddSingleton<IModelImportService, ModelImportService>();
            services.AddSingleton<IChatManagementService, ChatManagementService>();
            services.AddSingleton<IMediaSelectionService, MediaSelectionService>();

            // Pipeline and Node Management Services
            services.AddSingleton<NodeManagementService>();
            services.AddSingleton<PipelineManagementService>();
            services.AddSingleton<ActionReviewService>();
            services.AddSingleton<ActionStepNavigationService>();
            services.AddSingleton<ExecutionStatusTrackingService>();
            services.AddSingleton<IStepContentManagementService, StepContentManagementService>();
            services.AddSingleton<ICommandManagementService, CommandManagementService>();
            services.AddSingleton<IPipelineExecutionValidationService, PipelineExecutionValidationService>();
            services.AddSingleton<IModelLoadingManagementService, ModelLoadingManagementService>();
            services.AddSingleton<IActionReviewNavigationService, ActionReviewNavigationService>();
            services.AddSingleton<IFileManagementService, FileManagementService>();

            // EnsembleModelService with delayed NetPageViewModel lookup to avoid circular dependency
            services.AddSingleton<EnsembleModelService>(sp =>
            {
                return new EnsembleModelService(sp); // Pass the service provider to get NetPageViewModel when needed
            });

            // Pipeline Execution Service with factory pattern to avoid circular dependency
            services.AddSingleton<PipelineExecutionService>(sp =>
            {
                var ensembleModelService = sp.GetRequiredService<EnsembleModelService>();

                return new PipelineExecutionService(
                    ensembleModelService,
                    (node) =>
                    {
                        // Simple fallback - return null if no model found
                        // The PipelineExecutionService can handle null models gracefully
                        return null;
                    }
                );
            });

            // Application Mode and UI Services
            services.AddSingleton<AppModeService>();
            services.AddSingleton<ActionGroupService>();
            services.AddSingleton<GameSettingsService>();
            services.AddSingleton<ActionGroupCopierService>();
            services.AddSingleton<DialogService>();

            // Capture and Monitoring Services
            services.AddSingleton<ScreenCaptureService>();
            services.AddSingleton<AudioCaptureService>();
            services.AddSingleton<ObserveDataService>();
            services.AddSingleton<MouseTrackingService>();
            services.AddSingleton<VoiceAssistantService>();

            // Model Management Services
            services.AddSingleton<PythonEnvironmentService>();
            services.AddSingleton<ModelCommunicationService>();
            services.AddSingleton<ModelExecutionService>();
            services.AddSingleton<ModelImportExportService>();

            // Custom factory for InputCaptureService
            services.AddSingleton(sp =>
            {
                var actionService = sp.GetRequiredService<ActionService>();
                return new InputCaptureService(actionService);
            });

            return services;
        }

        public static IServiceCollection AddCSimpleViewModels(this IServiceCollection services)
        {
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<LoginViewModel>();
            services.AddSingleton<NetPageViewModel>();
            services.AddSingleton<OrientPageViewModel>();
            services.AddSingleton<ActionPageViewModel>();

            return services;
        }

        public static IServiceCollection AddCSimplePages(this IServiceCollection services)
        {
            // Simple pages
            services.AddSingleton<LoginPage>();
            services.AddSingleton(sp => new SettingsPage(
                sp.GetRequiredService<DataService>(),
                sp.GetRequiredService<IDebugConsoleService>(),
                sp.GetRequiredService<IAppPathService>()
            ));
            services.AddSingleton<NetPage>();
            services.AddSingleton<OrientPage>();

            // Custom factory for GoalPage with backend integration
            services.AddSingleton(sp => new GoalPage(
                sp.GetRequiredService<GoalService>(),
                sp.GetRequiredService<AppModeService>(),
                sp.GetRequiredService<OrientPageViewModel>(),
                sp.GetRequiredService<FileService>(),
                sp.GetRequiredService<DataService>(),
                sp.GetRequiredService<NetPageViewModel>()
            ));

            services.AddSingleton<ActionPage>();

            // Custom factory for HomePage
            services.AddSingleton(sp => new HomePage(
                sp.GetRequiredService<HomeViewModel>(),
                sp.GetRequiredService<DataService>(),
                sp.GetRequiredService<AppModeService>(),
                sp.GetRequiredService<VoiceAssistantService>()
            ));

            // Custom factory for ObservePage
            services.AddSingleton(sp => new ObservePage(
                sp.GetRequiredService<InputCaptureService>(),
                sp.GetRequiredService<ScreenCaptureService>(),
                sp.GetRequiredService<AudioCaptureService>(),
                sp.GetRequiredService<ObserveDataService>(),
                sp.GetRequiredService<MouseTrackingService>(),
                sp.GetRequiredService<ActionService>()
            ));

            return services;
        }

        public static IServiceCollection AddPlatformServices(this IServiceCollection services)
        {
#if WINDOWS
            services.AddSingleton<ITrayService, WinUI.TrayService>();
            services.AddSingleton<INotificationService, WinUI.NotificationService>();
            services.AddSingleton<IDebugConsoleService, Platforms.Windows.WindowsDebugConsoleService>();
            services.AddSingleton<IHotkeyService, Platforms.Windows.WindowsHotkeyService>();
#elif MACCATALYST
            services.AddSingleton<ITrayService, MacCatalyst.TrayService>();
            services.AddSingleton<INotificationService, MacCatalyst.NotificationService>();
            services.AddSingleton<IDebugConsoleService, DefaultDebugConsoleService>();
            services.AddSingleton<IHotkeyService, DefaultHotkeyService>();
#else
            services.AddSingleton<IDebugConsoleService, DefaultDebugConsoleService>();
            services.AddSingleton<IHotkeyService, DefaultHotkeyService>();
#endif
            return services;
        }
    }
}
