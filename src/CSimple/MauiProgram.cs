using CSimple.Extensions;

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

        // Register all services using extension methods
        services.AddCSimpleServices();
        services.AddCSimpleViewModels();
        services.AddCSimplePages();
        services.AddPlatformServices();

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